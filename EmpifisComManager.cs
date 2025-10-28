//using Empirija;
using NLog;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using System.Reflection;

namespace empifisJsonAPI2
{
    public class EmpifisComManager
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
    private Interop.Empirija.EmpiFisX? _comObject;
    private Exception? _lastInitException;
        private readonly int _comTimeoutSeconds;
        private bool _isInitializing = false;
    // When true, ExecuteComMethod will not attempt to auto-initialize the COM object.
    private bool _suppressAutoInit = false;
    // Prevent concurrent reloads
    private readonly object _reloadLock = new object();

        public EmpifisComManager(IOptions<AppConfig> config)
        {
            _comTimeoutSeconds = config.Value.servicePort.com_timeout_seconds;
            InitializeComObject();
        }

        private void InitializeComObject()
        {
            if (_isInitializing) return;
            _isInitializing = true;
            try
            {
                _logger.Info("Attempting to load the Empirija COM object.");
                _comObject = new Interop.Empirija.EmpiFisX();
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
            finally
            {
                _isInitializing = false;
            }
        }

        private int ExecuteComMethod(Func<int?> comMethod, string methodName)
        {
            if (_comObject == null)
            {
                _logger.Warn($"COM object is not initialized for '{methodName}'. Last init exception: {_lastInitException?.Message}");
                if (_suppressAutoInit)
                {
                    _logger.Warn($"Auto-initialization suppressed; refusing to initialize COM object for '{methodName}'.");
                    return 999;
                }

                _logger.Info($"Attempting to re-initialize COM object for '{methodName}'.");
                InitializeComObject();
                if (_comObject == null) return 999;
            }

            try
            {
                var task = Task.Run(comMethod);
                if (task.Wait(TimeSpan.FromSeconds(_comTimeoutSeconds)))
                {
                    _logger.Debug($"COM method '{methodName}' completed successfully.");
                    return task.Result ?? 999;
                }

                _logger.Warn($"COM method '{methodName}' timed out. Reloading object.");
                ReloadComObject();
                return 999;
            }
            catch (COMException ex)
            {
                _logger.Error(ex, $"COMException occurred during '{methodName}'. Reloading object.");
                ReloadComObject();
                return ex.ErrorCode;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"An unexpected error occurred during '{methodName}'. Reloading object.");
                ReloadComObject();
                return 999;
            }
        }

    public int ResetFiscal() => ExecuteComMethod(() => _comObject?.ResetFiscal(), nameof(ResetFiscal));
    public int PrintXReport() => ExecuteComMethod(() => _comObject?.PrintXReport(), nameof(PrintXReport));
    public int MoneyInCurr(int paymentType, double amount) => ExecuteComMethod(() => _comObject?.MoneyInCurr(paymentType, amount), nameof(MoneyInCurr));
    public int MoneyOutCurr(int paymentType, double amount) => ExecuteComMethod(() => _comObject?.MoneyOutCurr(paymentType, amount), nameof(MoneyOutCurr));
    public int OpenCashDrawer() => ExecuteComMethod(() => _comObject?.OpenCashDrawer(), nameof(OpenCashDrawer));
    public int SkipPrintReceipt() => ExecuteComMethod(() => _comObject?.SkipPrintReceipt(), nameof(SkipPrintReceipt));
    public int PrintZReport() => ExecuteComMethod(() => _comObject?.PrintZReport(), nameof(PrintZReport));
    public int PrintMiniXReport() => ExecuteComMethod(() => _comObject?.PrintMiniXReport(), nameof(PrintMiniXReport));
    public int PrintSumPeriodicReport(string dateFrom, string dateTo) => ExecuteComMethod(() => _comObject?.PrintSumPeriodicReport(dateFrom, dateTo), nameof(PrintSumPeriodicReport));
    public int PrintPeriodicReport(string dateFrom, string dateTo) => ExecuteComMethod(() => _comObject?.PrintPeriodicReport(dateFrom, dateTo), nameof(PrintPeriodicReport));
    public int PrintSumPeriodicReportByNumber(int noFrom, int noTo) => ExecuteComMethod(() => _comObject?.PrintSumPeriodicReportByNumber(noFrom, noTo), nameof(PrintSumPeriodicReportByNumber));
    public int PrintPeriodicReportByNumber(int noFrom, int noTo) => ExecuteComMethod(() => _comObject?.PrintPeriodicReportByNumber(noFrom, noTo), nameof(PrintPeriodicReportByNumber));
    public int CustomerDisplay2(string line1, string line2) => ExecuteComMethod(() => _comObject?.CustomerDisplay2(line1, line2), nameof(CustomerDisplay2));
    public int CustomerDisplayPro(string line) => ExecuteComMethod(() => _comObject?.CustomerDisplayPro(line), nameof(CustomerDisplayPro));
    public int BeginNonFiscalReceipt() => ExecuteComMethod(() => _comObject?.BeginNonFiscalReceipt(), nameof(BeginNonFiscalReceipt));
    public int PrintTareItem(string description, double quantity, double price) => ExecuteComMethod(() => _comObject?.PrintTareItem(description, quantity, price), nameof(PrintTareItem));
    public int PrintTareItemVoid(string description, double quantity, double price) => ExecuteComMethod(() => _comObject?.PrintTareItemVoid(description, quantity, price), nameof(PrintTareItemVoid));
    public int PrintTareDeposit(string description, double quantity, double unitPrice)
    {
        return ExecuteComMethod(() =>
        {
            if (_comObject == null) return (int?)null;
            dynamic comObj = _comObject;
            return (int?)comObj.PrintTareDeposit(description, quantity, unitPrice);
        }, nameof(PrintTareDeposit));
    }
    public int PrintTareDepositVoid(string description, double quantity, double unitPrice)
    {
        return ExecuteComMethod(() =>
        {
            if (_comObject == null) return (int?)null;
            dynamic comObj = _comObject;
            return (int?)comObj.PrintTareDepositVoid(description, quantity, unitPrice);
        }, nameof(PrintTareDepositVoid));
    }
    public int PrintDepositReceive(string description, double quantity, double price) => ExecuteComMethod(() => _comObject?.PrintDepositReceive(description, quantity, price), nameof(PrintDepositReceive));
    public int PrintDepositReceiveCredit(string description, double quantity, double price) => ExecuteComMethod(() => _comObject?.PrintDepositReceiveCredit(description, quantity, price), nameof(PrintDepositReceiveCredit));
    public int PrintDepositRefund(string description, double quantity, double price) => ExecuteComMethod(() => _comObject?.PrintDepositRefund(description, quantity, price), nameof(PrintDepositRefund));
    public int PrintBarCode(int system, int height, string barCode) => ExecuteComMethod(() => _comObject?.PrintBarCode(system, height, barCode), nameof(PrintBarCode));
    public int PrintNonFiscalLine(string line, int attrib) => ExecuteComMethod(() => _comObject?.PrintNonFiscalLine(line, attrib), nameof(PrintNonFiscalLine));
    public int EndNonFiscalReceipt() => ExecuteComMethod(() => _comObject?.EndNonFiscalReceipt(), nameof(EndNonFiscalReceipt));
    public int BeginFiscalReceipt() => ExecuteComMethod(() => _comObject?.BeginFiscalReceipt(), nameof(BeginFiscalReceipt));
    public int PrintRecItem(string itemDescription, double itemQuantity, double itemPrice, int vatID, string itemUnit) => ExecuteComMethod(() => _comObject?.PrintRecItem(itemDescription, itemQuantity, itemPrice, vatID, itemUnit), nameof(PrintRecItem));
    public int PrintRecItemEx(string description, double quantity, double price, int vat, string dimension, string group) => ExecuteComMethod(() => _comObject?.PrintRecItemEx(description, quantity, price, vat, dimension, group), nameof(PrintRecItemEx));
    public int ItemReturn(string description, double quantity, double price, int vat, string dimension, double currPercent, double currAbsolute) => ExecuteComMethod(() => _comObject?.ItemReturn(description, quantity, price, vat, dimension, currPercent, currAbsolute), nameof(ItemReturn));
    public int ItemReturnEx(string description, double quantity, double price, int vat, string dimension, string group, double currPercent, double currAbsolute) => ExecuteComMethod(() => _comObject?.ItemReturnEx(description, quantity, price, vat, dimension, group, currPercent, currAbsolute), nameof(ItemReturnEx));
    public int PrintDepositReceiveVoid(string description, double quantity, double price) => ExecuteComMethod(() => _comObject?.PrintDepositReceiveVoid(description, quantity, price), nameof(PrintDepositReceiveVoid));
    public int PrintCommentLine(string commentLine, int commentLineAttrib) => ExecuteComMethod(() => _comObject?.PrintCommentLine(commentLine, commentLineAttrib), nameof(PrintCommentLine));
    public int DiscountAdditionForItem(int type, double amount) => ExecuteComMethod(() => _comObject?.DiscountAdditionForItem(type, amount), nameof(DiscountAdditionForItem));
    public int DiscountAdditionForReceipt(int type, double amount) => ExecuteComMethod(() => _comObject?.DiscountAdditionForReceipt(type, amount), nameof(DiscountAdditionForReceipt));
    public int TransferPreReceipt(string receiptNo, double amount) => ExecuteComMethod(() => _comObject?.TransferPreReceipt(receiptNo, amount), nameof(TransferPreReceipt));
    public int EndPreReceipt() => ExecuteComMethod(() => _comObject?.EndPreReceipt(), nameof(EndPreReceipt));
    public int LinkPreReceipt(string receiptNo, double amount) => ExecuteComMethod(() => _comObject?.LinkPreReceipt(receiptNo, amount), nameof(LinkPreReceipt));
    public int EndFiscalReceiptCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => ExecuteComMethod(() => _comObject?.EndFiscalReceiptCurr(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), nameof(EndFiscalReceiptCurr));
    public int SetCustomerContact(string contact) => ExecuteComMethod(() => _comObject?.SetCustomerContact(contact), nameof(SetCustomerContact));
    public int RefundReceiptInfo(string eCR, string receiptNo, string docNo) => ExecuteComMethod(() => _comObject?.RefundReceiptInfo(eCR, receiptNo, docNo), nameof(RefundReceiptInfo));
    public int GoodsReturnCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => ExecuteComMethod(() => _comObject?.GoodsReturnCurr(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), nameof(GoodsReturnCurr));
    public int EndFiscalReceiptEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => ExecuteComMethod(() => _comObject?.EndFiscalReceiptEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), nameof(EndFiscalReceiptEx));
    public int GoodsReturnEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => ExecuteComMethod(() => _comObject?.GoodsReturnEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), nameof(GoodsReturnEx));
    public int EndRecPaymentEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => ExecuteComMethod(() => _comObject?.EndRecPaymentEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), nameof(EndRecPaymentEx));
    public int EndFiscalCacheReceipt() => ExecuteComMethod(() => _comObject?.EndFiscalCacheReceipt(), nameof(EndFiscalCacheReceipt));
    public int GoodsReturnCacheReceipt() => ExecuteComMethod(() => _comObject?.GoodsReturnCacheReceipt(), nameof(GoodsReturnCacheReceipt));
    public int EndRecPayment(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => ExecuteComMethod(() => _comObject?.EndRecPayment(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), nameof(EndRecPayment));
    public int PrintCopyOfLastReceipt() => ExecuteComMethod(() => _comObject?.PrintCopyOfLastReceipt(), nameof(PrintCopyOfLastReceipt));
    public int PrintCopyOfReceipt(int from, int to) => ExecuteComMethod(() => _comObject?.PrintCopyOfReceipt(from, to), nameof(PrintCopyOfReceipt));
    public int SetFooter(string line1, string line2, string line3, string line4) => ExecuteComMethod(() => _comObject?.SetFooter(64, line1, 64, line2, 64, line3, 64, line4), nameof(SetFooter));

        public (int errorCode, string message) GetFiscalInfo(int infoType)
        {
            string message = "";
            int errorCode = 999;

            if (_comObject == null)
            {
                _logger.Warn("COM object is not initialized. Attempting to re-initialize.");
                InitializeComObject();
                if (_comObject == null)
                {
                    return (errorCode, "COM object could not be initialized.");
                }
            }

            try
            {
                var task = Task.Run(() =>
                {
                    string tempMessage = "";
                    int tempCode = _comObject.GetFiscalInfo(infoType, ref tempMessage);
                    return (tempCode, tempMessage);
                });

                if (task.Wait(TimeSpan.FromSeconds(_comTimeoutSeconds)))
                {
                    errorCode = task.Result.tempCode;
                    message = task.Result.tempMessage;
                    _logger.Debug("COM method GetFiscalInfo completed successfully.");
                }
                else
                {
                    _logger.Warn("COM method GetFiscalInfo timed out. Reloading object.");
                    ReloadComObject();
                    message = "COM method call timed out.";
                }
            }
            catch (COMException ex)
            {
                _logger.Error(ex, "COMException occurred during GetFiscalInfo. Reloading object.");
                ReloadComObject();
                errorCode = ex.ErrorCode;
                message = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An unexpected error occurred during GetFiscalInfo. Reloading object.");
                ReloadComObject();
                message = ex.Message;
            }

            return (errorCode, message);
        }

        private void ReloadComObject()
        {
            lock (_reloadLock)
            {
                _logger.Info("Reloading the Empirija COM object (Unload then Load).");
                try
                {
                    // Explicitly unload then load using the public helpers so suppression flags
                    // and logging behavior are consistent.
                    Unload();
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Exception during Unload() as part of ReloadComObject().");
                }

                try
                {
                    // Ensure Load re-enables auto-init and attempts initialization
                    bool loaded = Load();
                    if (!loaded)
                    {
                        _logger.Warn("ReloadComObject: Load() completed but COM object is still not loaded.");
                    }
                    else
                    {
                        _logger.Info("ReloadComObject: COM object loaded successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _lastInitException = ex;
                    _logger.Error(ex, "Exception during Load() as part of ReloadComObject().");
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
            _suppressAutoInit = true;
            if (_comObject != null)
            {
                try
                {
                    Marshal.ReleaseComObject(_comObject);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Exception while releasing COM object during Unload().");
                }
                finally
                {
                    _comObject = null;
                }
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

        public void Dispose()
        {
            if (_comObject != null)
            {
                Marshal.ReleaseComObject(_comObject);
                _comObject = null;
            }
        }
    }
}