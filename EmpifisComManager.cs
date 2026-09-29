//using Empirija;
using NLog;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using System.Reflection;

namespace empifisJsonAPI2
{
    public class EmpifisComManager : IFiscalDevice, IDisposable
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        // EmpiFisX is registered as apartment-threaded, so it is created, called and released only on
        // its own STA thread. A hung call blocks that thread, not the service: on a timeout the thread
        // is abandoned and a new thread with a new object takes over.
        private ComStaThread? _comThread;
        private Interop.Empirija.EmpiFisX? _comObject;
        private Exception? _lastInitException;
        private readonly int _comTimeoutSeconds;
        // When true, COM calls will not attempt to auto-initialize the COM object.
        private bool _suppressAutoInit = false;
        // Serializes creating, reloading and releasing the COM object.
        private readonly object _reloadLock = new object();
        // Serializes whole device operations (a full receipt or one /fiscalCommand) across HTTP and file-watcher callers.
        private readonly SemaphoreSlim _deviceLock = new SemaphoreSlim(1, 1);

        public EmpifisComManager(IOptions<AppConfig> config)
        {
            _comTimeoutSeconds = config.Value.servicePort.com_timeout_seconds;
            InitializeComObject();
        }

        private void InitializeComObject()
        {
            lock (_reloadLock)
            {
                if (_comObject != null) return;

                _comThread ??= new ComStaThread("EmpiFisX COM");
                var comThread = _comThread;
                _logger.Info("Attempting to load the Empirija COM object.");
                var create = comThread.Invoke(() => new Interop.Empirija.EmpiFisX());

                if (!WaitForComCall(create))
                {
                    _lastInitException = new TimeoutException($"Creating the Empirija COM object did not finish within {_comTimeoutSeconds} s.");
                    _logger.Error($"{_lastInitException.Message} Abandoning its COM thread.");
                    // Release the object if the creation ever completes.
                    comThread.Shutdown(() => create.IsCompletedSuccessfully ? create.Result : null);
                    _comThread = null;
                    return;
                }

                try
                {
                    _comObject = create.GetAwaiter().GetResult();
                    _lastInitException = null;
                    _logger.Info("Empirija COM object loaded successfully.");
                }
                catch (Exception ex)
                {
                    // Capture full exception (message + stack) for diagnostics and log it.
                    _lastInitException = ex;
                    _logger.Error(ex, "Failed to load the Empirija COM object.");
                    _comObject = null;
                }
            }
        }

        private bool EnsureComObject(string methodName)
        {
            if (_comObject != null) return true;

            _logger.Warn($"COM object is not initialized for '{methodName}'. Last init exception: {_lastInitException?.Message}");
            if (_suppressAutoInit)
            {
                _logger.Warn($"Auto-initialization suppressed; refusing to initialize COM object for '{methodName}'.");
                return false;
            }

            _logger.Info($"Attempting to re-initialize COM object for '{methodName}'.");
            InitializeComObject();
            return _comObject != null;
        }

        private bool WaitForComCall(Task call) =>
            ((IAsyncResult)call).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(_comTimeoutSeconds));

        /// <summary>
        /// Runs <paramref name="comCall"/> on the COM thread with the configured timeout. Failures are
        /// turned into a result by <paramref name="failure"/> (error code, message): 999 = not loaded or
        /// unexpected error, 998 = method missing from the installed EmpiFisX, 555/556 = timed out and the
        /// object was / wasn't reloaded, otherwise the COMException's error code.
        /// </summary>
        private T ExecuteCom<T>(Func<Interop.Empirija.EmpiFisX, T> comCall, string methodName, Func<int, string, T> failure)
        {
            if (!EnsureComObject(methodName)) return failure(999, "COM object could not be initialized.");

            ComStaThread? comThread;
            Interop.Empirija.EmpiFisX? comObject;
            lock (_reloadLock)
            {
                comThread = _comThread;
                comObject = _comObject;
            }
            if (comThread == null || comObject == null) return failure(999, "COM object could not be initialized.");

            var call = comThread.Invoke(() => comCall(comObject));
            if (!WaitForComCall(call))
            {
                _logger.Warn($"COM method '{methodName}' timed out after {_comTimeoutSeconds} s. Abandoning its COM thread and reloading the object.");
                var reloadSuccess = ReloadComObject(comThreadHung: true);
                // 555 = reload successful, 556 = reload unsuccessful
                return failure(reloadSuccess ? 555 : 556, "COM method call timed out.");
            }

            try
            {
                var result = call.GetAwaiter().GetResult();
                _logger.Debug($"COM method '{methodName}' completed successfully.");
                return result;
            }
            catch (Exception ex) when (IsMissingComMember(ex))
            {
                // A method this EmpiFisX.dll doesn't have (see the late-bound methods below). This is an
                // installation problem, not a device fault, so don't reload the COM object for it.
                _logger.Error($"COM method '{methodName}' is not implemented by the installed EmpiFisX.dll. " +
                    "Update EmpiFisX on this machine before using this command.");
                return failure(998, $"'{methodName}' is not supported by the installed EmpiFisX.dll.");
            }
            catch (COMException ex)
            {
                _logger.Error(ex, $"COMException occurred during '{methodName}'. Reloading object.");
                ReloadComObject();
                return failure(ex.ErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"An unexpected error occurred during '{methodName}'. Reloading object.");
                ReloadComObject();
                return failure(999, ex.Message);
            }
        }

        private int ExecuteComMethod(Func<Interop.Empirija.EmpiFisX, int> comCall, string methodName) =>
            ExecuteCom(comCall, methodName, (errorCode, _) => errorCode);

        // Late-bound calls (dynamic, used for methods absent from the compiled interop type) throw
        // RuntimeBinderException when the COM object's IDispatch doesn't recognize the member name.
        private static bool IsMissingComMember(Exception ex)
        {
            var inner = ex is AggregateException agg ? agg.InnerException : ex;
            return inner is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;
        }

        public int ResetFiscal() => ExecuteComMethod(com => com.ResetFiscal(), nameof(ResetFiscal));
        public int PrintXReport() => ExecuteComMethod(com => com.PrintXReport(), nameof(PrintXReport));
        public int MoneyInCurr(int paymentType, double amount) => ExecuteComMethod(com => com.MoneyInCurr(paymentType, amount), nameof(MoneyInCurr));
        public int MoneyOutCurr(int paymentType, double amount) => ExecuteComMethod(com => com.MoneyOutCurr(paymentType, amount), nameof(MoneyOutCurr));
        public int OpenCashDrawer() => ExecuteComMethod(com => com.OpenCashDrawer(), nameof(OpenCashDrawer));
        public int SkipPrintReceipt() => ExecuteComMethod(com => com.SkipPrintReceipt(), nameof(SkipPrintReceipt));
        public int PrintZReport() => ExecuteComMethod(com => com.PrintZReport(), nameof(PrintZReport));
        public int PrintMiniXReport() => ExecuteComMethod(com => com.PrintMiniXReport(), nameof(PrintMiniXReport));
        public int PrintSumPeriodicReport(string dateFrom, string dateTo) => ExecuteComMethod(com => com.PrintSumPeriodicReport(dateFrom, dateTo), nameof(PrintSumPeriodicReport));
        public int PrintPeriodicReport(string dateFrom, string dateTo) => ExecuteComMethod(com => com.PrintPeriodicReport(dateFrom, dateTo), nameof(PrintPeriodicReport));
        public int PrintSumPeriodicReportByNumber(int noFrom, int noTo) => ExecuteComMethod(com => com.PrintSumPeriodicReportByNumber(noFrom, noTo), nameof(PrintSumPeriodicReportByNumber));
        public int PrintPeriodicReportByNumber(int noFrom, int noTo) => ExecuteComMethod(com => com.PrintPeriodicReportByNumber(noFrom, noTo), nameof(PrintPeriodicReportByNumber));
        public int CustomerDisplay2(string line1, string line2) => ExecuteComMethod(com => com.CustomerDisplay2(line1, line2), nameof(CustomerDisplay2));
        public int CustomerDisplayPro(string line) => ExecuteComMethod(com => com.CustomerDisplayPro(line), nameof(CustomerDisplayPro));
        public int BeginNonFiscalReceipt() => ExecuteComMethod(com => com.BeginNonFiscalReceipt(), nameof(BeginNonFiscalReceipt));
        public int PrintTareItem(string description, double quantity, double price) => ExecuteComMethod(com => com.PrintTareItem(description, quantity, price), nameof(PrintTareItem));
        public int PrintTareItemVoid(string description, double quantity, double price) => ExecuteComMethod(com => com.PrintTareItemVoid(description, quantity, price), nameof(PrintTareItemVoid));
        public int PrintDepositReceive(string description, double quantity, double price) => ExecuteComMethod(com => com.PrintDepositReceive(description, quantity, price), nameof(PrintDepositReceive));
        public int PrintDepositReceiveCredit(string description, double quantity, double price) => ExecuteComMethod(com => com.PrintDepositReceiveCredit(description, quantity, price), nameof(PrintDepositReceiveCredit));
        public int PrintDepositRefund(string description, double quantity, double price) => ExecuteComMethod(com => com.PrintDepositRefund(description, quantity, price), nameof(PrintDepositRefund));
        public int PrintBarCode(int system, int height, string barCode) => ExecuteComMethod(com => com.PrintBarCode(system, height, barCode), nameof(PrintBarCode));
        public int PrintNonFiscalLine(string line, int attrib) => ExecuteComMethod(com => com.PrintNonFiscalLine(line, attrib), nameof(PrintNonFiscalLine));
        public int EndNonFiscalReceipt() => ExecuteComMethod(com => com.EndNonFiscalReceipt(), nameof(EndNonFiscalReceipt));
        public int BeginFiscalReceipt() => ExecuteComMethod(com => com.BeginFiscalReceipt(), nameof(BeginFiscalReceipt));
        public int PrintRecItem(string itemDescription, double itemQuantity, double itemPrice, int vatID, string itemUnit) => ExecuteComMethod(com => com.PrintRecItem(itemDescription, itemQuantity, itemPrice, vatID, itemUnit), nameof(PrintRecItem));
        public int PrintRecItemEx(string description, double quantity, double price, int vat, string dimension, string group) => ExecuteComMethod(com => com.PrintRecItemEx(description, quantity, price, vat, dimension, group), nameof(PrintRecItemEx));
        public int ItemReturnEx(string description, double quantity, double price, int vat, string dimension, string group, double currPercent, double currAbsolute) => ExecuteComMethod(com => com.ItemReturnEx(description, quantity, price, vat, dimension, group, currPercent, currAbsolute), nameof(ItemReturnEx));
        public int PrintDepositReceiveVoid(string description, double quantity, double price) => ExecuteComMethod(com => com.PrintDepositReceiveVoid(description, quantity, price), nameof(PrintDepositReceiveVoid));
        public int PrintCommentLine(string commentLine, int commentLineAttrib) => ExecuteComMethod(com => com.PrintCommentLine(commentLine, commentLineAttrib), nameof(PrintCommentLine));
        public int DiscountAdditionForItem(int type, double amount) => ExecuteComMethod(com => com.DiscountAdditionForItem(type, amount), nameof(DiscountAdditionForItem));
        public int DiscountAdditionForReceipt(int type, double amount) => ExecuteComMethod(com => com.DiscountAdditionForReceipt(type, amount), nameof(DiscountAdditionForReceipt));
        public int TransferPreReceipt(string receiptNo, double amount) => ExecuteComMethod(com => com.TransferPreReceipt(receiptNo, amount), nameof(TransferPreReceipt));
        public int EndPreReceipt() => ExecuteComMethod(com => com.EndPreReceipt(), nameof(EndPreReceipt));
        public int LinkPreReceipt(string receiptNo, double amount) => ExecuteComMethod(com => com.LinkPreReceipt(receiptNo, amount), nameof(LinkPreReceipt));
        public int EndFiscalReceiptCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => ExecuteComMethod(com => com.EndFiscalReceiptCurr(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), nameof(EndFiscalReceiptCurr));
        public int SetCustomerContact(string contact) => ExecuteComMethod(com => com.SetCustomerContact(contact), nameof(SetCustomerContact));
        public int RefundReceiptInfo(string eCR, string receiptNo, string docNo) => ExecuteComMethod(com => com.RefundReceiptInfo(eCR, receiptNo, docNo), nameof(RefundReceiptInfo));
        public int GoodsReturnCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => ExecuteComMethod(com => com.GoodsReturnCurr(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), nameof(GoodsReturnCurr));
        public int EndFiscalReceiptEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => ExecuteComMethod(com => com.EndFiscalReceiptEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), nameof(EndFiscalReceiptEx));
        public int GoodsReturnEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => ExecuteComMethod(com => com.GoodsReturnEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), nameof(GoodsReturnEx));
        public int EndRecPaymentEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => ExecuteComMethod(com => com.EndRecPaymentEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), nameof(EndRecPaymentEx));
        public int EndFiscalCacheReceipt() => ExecuteComMethod(com => com.EndFiscalCacheReceipt(), nameof(EndFiscalCacheReceipt));
        public int GoodsReturnCacheReceipt() => ExecuteComMethod(com => com.GoodsReturnCacheReceipt(), nameof(GoodsReturnCacheReceipt));
        public int EndRecPayment(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => ExecuteComMethod(com => com.EndRecPayment(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), nameof(EndRecPayment));
        public int PrintCopyOfLastReceipt() => ExecuteComMethod(com => com.PrintCopyOfLastReceipt(), nameof(PrintCopyOfLastReceipt));
        public int PrintCopyOfReceipt(int from, int to) => ExecuteComMethod(com => com.PrintCopyOfReceipt(from, to), nameof(PrintCopyOfReceipt));
        public int SetFooter(string line1, string line2, string line3, string line4) => ExecuteComMethod(com => com.SetFooter(64, line1, 64, line2, 64, line3, 64, line4), nameof(SetFooter));

        // PrintTareDeposit(Void), EndFiscalReceiptPayment, GoodsReturnPayment and EndCacheReceipt are newer
        // EmpiFisX methods (present in 5.0.2.370) that the checked-in Interop.Empirija.dll doesn't declare.
        // They are called late-bound through IDispatch on purpose: IEmpiFisX is a dual interface, so a typed
        // (vtable) call to a method an older EmpiFisX.dll lacks would jump past the end of its vtable and
        // crash the process, while a late-bound call fails cleanly with 998. Don't regenerate the interop
        // to turn these into typed calls.
        public int PrintTareDeposit(string description, double quantity, double unitPrice) =>
            ExecuteComMethod(com => (int)((dynamic)com).PrintTareDeposit(description, quantity, unitPrice), nameof(PrintTareDeposit));
        public int PrintTareDepositVoid(string description, double quantity, double unitPrice) =>
            ExecuteComMethod(com => (int)((dynamic)com).PrintTareDepositVoid(description, quantity, unitPrice), nameof(PrintTareDepositVoid));
        public int EndFiscalReceiptPayment(double aCash, double aCredit1, double aCredit2, double aCredit3, double aCredit4, double aCredit5, double aCredit6, double aCredit7, double aCredit8) =>
            ExecuteComMethod(com => (int)((dynamic)com).EndFiscalReceiptPayment(aCash, aCredit1, aCredit2, aCredit3, aCredit4, aCredit5, aCredit6, aCredit7, aCredit8), nameof(EndFiscalReceiptPayment));
        public int GoodsReturnPayment(double aCash, double aCredit1, double aCredit2, double aCredit3, double aCredit4, double aCredit5, double aCredit6, double aCredit7, double aCredit8) =>
            ExecuteComMethod(com => (int)((dynamic)com).GoodsReturnPayment(aCash, aCredit1, aCredit2, aCredit3, aCredit4, aCredit5, aCredit6, aCredit7, aCredit8), nameof(GoodsReturnPayment));
        public int EndCacheReceipt() =>
            ExecuteComMethod(com => (int)((dynamic)com).EndCacheReceipt(), nameof(EndCacheReceipt));

        public (int errorCode, string result) GetCopyOfReceipt(int from, int to) => ExecuteCom(com =>
        {
            string result = string.Empty;
            int errorCode = com.GetCopyOfReceipt(from, to, ref result);
            return (errorCode, result ?? string.Empty);
        }, nameof(GetCopyOfReceipt), (errorCode, message) => (errorCode, message));

        public (int errorCode, string message) GetFiscalInfo(int infoType) => ExecuteCom(com =>
        {
            string message = string.Empty;
            int errorCode = com.GetFiscalInfo(infoType, ref message);
            return (errorCode, message ?? string.Empty);
        }, nameof(GetFiscalInfo), (errorCode, message) => (errorCode, message));

        private bool ReloadComObject(bool comThreadHung = false)
        {
            lock (_reloadLock)
            {
                _logger.Info("Reloading the Empirija COM object.");
                ReleaseComObject(waitForRelease: !comThreadHung);

                _suppressAutoInit = false;
                InitializeComObject();
                bool loaded = _comObject != null;
                if (loaded)
                {
                    _logger.Info("ReloadComObject: COM object loaded successfully.");
                }
                else
                {
                    _logger.Warn("ReloadComObject: COM object is still not loaded.");
                }
                return loaded;
            }
        }

        // Releases the object on the thread that owns it and ends that thread. When the thread is stuck
        // in a hung call, don't wait: the release runs whenever (if ever) the call returns, and a new
        // thread is used from now on.
        private void ReleaseComObject(bool waitForRelease, TimeSpan? maxWait = null)
        {
            lock (_reloadLock)
            {
                var oldThread = _comThread;
                var oldObject = _comObject;
                _comThread = null;
                _comObject = null;
                if (oldThread == null) return;

                var shutdown = oldThread.Shutdown(() => oldObject);
                if (!waitForRelease) return;

                bool released = maxWait.HasValue
                    ? ((IAsyncResult)shutdown).AsyncWaitHandle.WaitOne(maxWait.Value)
                    : WaitForComCall(shutdown);
                if (!released)
                {
                    _logger.Warn("Releasing the Empirija COM object did not finish in time; continuing without waiting.");
                }
                else if (shutdown.IsFaulted)
                {
                    _logger.Warn(shutdown.Exception, "Exception while releasing the Empirija COM object.");
                }
            }
        }

        // Public control methods for diagnostics/tests
        /// <summary>
        /// Force unload the COM object without attempting to re-initialize.
        /// Useful for testing unload/reload behaviour.
        /// </summary>
        public void Unload()
        {
            _logger.Info("Unloading the Empirija COM object on user request.");
            lock (_reloadLock)
            {
                _suppressAutoInit = true;
                ReleaseComObject(waitForRelease: true);
            }
        }

        /// <summary>
        /// Force load/reload the COM object explicitly and return whether it is loaded.
        /// This also reenables auto-initialization.
        /// </summary>
        public bool Load()
        {
            _logger.Info("Explicitly loading the Empirija COM object on user request.");
            _suppressAutoInit = false;
            InitializeComObject();
            return _comObject != null;
        }

        /// <summary>
        /// Returns true if the COM object is currently loaded.
        /// </summary>
        public bool IsLoaded() => _comObject != null;

        /// <summary>
        /// Waits for exclusive use of the fiscal device; dispose the result to release it.
        /// Hold it for a whole receipt so concurrent requests can't interleave their COM calls.
        /// Not reentrant: acquire it only at entry points, never inside ReceiptProcessor.
        /// </summary>
        public async Task<IDisposable> AcquireDeviceLockAsync()
        {
            await _deviceLock.WaitAsync();
            return new DeviceLockReleaser(_deviceLock);
        }

        private sealed class DeviceLockReleaser : IDisposable
        {
            private SemaphoreSlim? _semaphore;
            public DeviceLockReleaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
            public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
        }

        // Called by the DI container on host shutdown. Bounded so a stuck device can't hold up a
        // Windows service stop past the time the Service Control Manager allows.
        public void Dispose() => ReleaseComObject(waitForRelease: true, maxWait: TimeSpan.FromSeconds(5));
    }
}
