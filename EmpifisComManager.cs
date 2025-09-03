using Empirija;
using NLog;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace empifisJsonAPI2
{
    public class EmpifisComManager
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        private Empirija.EmpiFisX _comObject;
        private readonly int _comTimeoutSeconds;
        private bool _isInitializing = false;

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
                _comObject = new Empirija.EmpiFisX();
                _logger.Info("Empirija COM object loaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load the Empirija COM object.");
                _comObject = null;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        // Generic method to wrap COM calls with no arguments
        private int CallComMethod(Func<int> comMethod, string methodName)
        {
            if (_comObject == null)
            {
                _logger.Warn($"COM object is not initialized. Attempting to re-initialize for '{methodName}'.");
                InitializeComObject();
                if (_comObject == null) return 999;
            }
            try
            {
                var task = Task.Run(comMethod);
                if (task.Wait(TimeSpan.FromSeconds(_comTimeoutSeconds))) return task.Result;
                ReloadComObject();
                return 999;
            }
            catch (COMException ex) { _logger.Error(ex, $"COMException occurred during '{methodName}'. Reloading object."); ReloadComObject(); return ex.ErrorCode; }
            catch (Exception ex) { _logger.Error(ex, $"An unexpected error occurred during '{methodName}'. Reloading object."); ReloadComObject(); return 999; }
        }

        // Generic method to wrap COM calls with arguments
        private int CallComMethodWithArgs(Func<int> comMethodWithArgs, string methodName)
        {
            if (_comObject == null)
            {
                _logger.Warn($"COM object is not initialized. Attempting to re-initialize for '{methodName}'.");
                InitializeComObject();
                if (_comObject == null) return 999;
            }
            try
            {
                var task = Task.Run(comMethodWithArgs);
                if (task.Wait(TimeSpan.FromSeconds(_comTimeoutSeconds))) return task.Result;
                ReloadComObject();
                return 999;
            }
            catch (COMException ex) { _logger.Error(ex, $"COMException occurred during '{methodName}'. Reloading object."); ReloadComObject(); return ex.ErrorCode; }
            catch (Exception ex) { _logger.Error(ex, $"An unexpected error occurred during '{methodName}'. Reloading object."); ReloadComObject(); return 999; }
        }

        // Dedicated wrapper methods for each COM call
        public int ResetFiscal() => CallComMethod(() => _comObject.ResetFiscal(), "ResetFiscal");
        public int PrintXReport() => CallComMethod(() => _comObject.PrintXReport(), "PrintXReport");
        public int MoneyInCurr(int paymentType, double amount) => CallComMethodWithArgs(() => _comObject.MoneyInCurr(paymentType, amount), "MoneyInCurr");
        public int MoneyOutCurr(int paymentType, double amount) => CallComMethodWithArgs(() => _comObject.MoneyOutCurr(paymentType, amount), "MoneyOutCurr");
        public int OpenCashDrawer() => CallComMethod(() => _comObject.OpenCashDrawer(), "OpenCashDrawer");
        public int SkipPrintReceipt() => CallComMethod(() => _comObject.SkipPrintReceipt(), "SkipPrintReceipt");
        public int PrintZReport() => CallComMethod(() => _comObject.PrintZReport(), "PrintZReport");
        public int PrintMiniXReport() => CallComMethod(() => _comObject.PrintMiniXReport(), "PrintMiniXReport");
        public int PrintSumPeriodicReport(string dateFrom, string dateTo) => CallComMethodWithArgs(() => _comObject.PrintSumPeriodicReport(dateFrom, dateTo), "PrintSumPeriodicReport");
        public int PrintPeriodicReport(string dateFrom, string dateTo) => CallComMethodWithArgs(() => _comObject.PrintPeriodicReport(dateFrom, dateTo), "PrintPeriodicReport");
        public int PrintSumPeriodicReportByNumber(int noFrom, int noTo) => CallComMethodWithArgs(() => _comObject.PrintSumPeriodicReportByNumber(noFrom, noTo), "PrintSumPeriodicReportByNumber");
        public int PrintPeriodicReportByNumber(int noFrom, int noTo) => CallComMethodWithArgs(() => _comObject.PrintPeriodicReportByNumber(noFrom, noTo), "PrintPeriodicReportByNumber");
        public int CustomerDisplay2(string line1, string line2) => CallComMethodWithArgs(() => _comObject.CustomerDisplay2(line1, line2), "CustomerDisplay2");
        public int CustomerDisplayPro(string line) => CallComMethodWithArgs(() => _comObject.CustomerDisplayPro(line), "CustomerDisplayPro");
        public int BeginNonFiscalReceipt() => CallComMethod(() => _comObject.BeginNonFiscalReceipt(), "BeginNonFiscalReceipt");
        public int PrintTareItem(string description, double quantity, double price) => CallComMethodWithArgs(() => _comObject.PrintTareItem(description, quantity, price), "PrintTareItem");
        public int PrintTareItemVoid(string description, double quantity, double price) => CallComMethodWithArgs(() => _comObject.PrintTareItemVoid(description, quantity, price), "PrintTareItemVoid");
        public int PrintDepositReceive(string description, double quantity, double price) => CallComMethodWithArgs(() => _comObject.PrintDepositReceive(description, quantity, price), "PrintDepositReceive");
        public int PrintDepositReceiveCredit(string description, double quantity, double price) => CallComMethodWithArgs(() => _comObject.PrintDepositReceiveCredit(description, quantity, price), "PrintDepositReceiveCredit");
        public int PrintDepositRefund(string description, double quantity, double price) => CallComMethodWithArgs(() => _comObject.PrintDepositRefund(description, quantity, price), "PrintDepositRefund");
        public int PrintBarCode(int system, int height, string barCode) => CallComMethodWithArgs(() => _comObject.PrintBarCode(system, height, barCode), "PrintBarCode");
        public int PrintNonFiscalLine(string line, int attrib) => CallComMethodWithArgs(() => _comObject.PrintNonFiscalLine(line, attrib), "PrintNonFiscalLine");
        public int EndNonFiscalReceipt() => CallComMethod(() => _comObject.EndNonFiscalReceipt(), "EndNonFiscalReceipt");
        public int BeginFiscalReceipt() => CallComMethod(() => _comObject.BeginFiscalReceipt(), "BeginFiscalReceipt");
        public int PrintRecItem(string itemDescription, double itemQuantity, double itemPrice, int vatID, string itemUnit) => CallComMethodWithArgs(() => _comObject.PrintRecItem(itemDescription, itemQuantity, itemPrice, vatID, itemUnit), "PrintRecItem");
        public int PrintRecItemEx(string description, double quantity, double price, int vat, string dimension, string group) => CallComMethodWithArgs(() => _comObject.PrintRecItemEx(description, quantity, price, vat, dimension, group), "PrintRecItemEx");
        public int ItemReturn(string description, double quantity, double price, int vat, string dimension, double currPercent, double currAbsolute) => CallComMethodWithArgs(() => _comObject.ItemReturn(description, quantity, price, vat, dimension, currPercent, currAbsolute), "ItemReturn");
        public int ItemReturnEx(string description, double quantity, double price, int vat, string dimension, string group, double currPercent, double currAbsolute) => CallComMethodWithArgs(() => _comObject.ItemReturnEx(description, quantity, price, vat, dimension, group, currPercent, currAbsolute), "ItemReturnEx");
        public int PrintDepositReceiveVoid(string description, double quantity, double price) => CallComMethodWithArgs(() => _comObject.PrintDepositReceiveVoid(description, quantity, price), "PrintDepositReceiveVoid");
        public int PrintCommentLine(string commentLine, int commentLineAttrib) => CallComMethodWithArgs(() => _comObject.PrintCommentLine(commentLine, commentLineAttrib), "PrintCommentLine");
        public int DiscountAdditionForItem(int type, double amount) => CallComMethodWithArgs(() => _comObject.DiscountAdditionForItem(type, amount), "DiscountAdditionForItem");
        public int DiscountAdditionForReceipt(int type, double amount) => CallComMethodWithArgs(() => _comObject.DiscountAdditionForReceipt(type, amount), "DiscountAdditionForReceipt");
        public int TransferPreReceipt(string receiptNo, double amount) => CallComMethodWithArgs(() => _comObject.TransferPreReceipt(receiptNo, amount), "TransferPreReceipt");
        public int EndPreReceipt() => CallComMethod(() => _comObject.EndPreReceipt(), "EndPreReceipt");
        public int LinkPreReceipt(string receiptNo, double amount) => CallComMethodWithArgs(() => _comObject.LinkPreReceipt(receiptNo, amount), "LinkPreReceipt");
        public int EndFiscalReceiptCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => CallComMethodWithArgs(() => _comObject.EndFiscalReceiptCurr(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), "EndFiscalReceiptCurr");
        public int SetCustomerContact(string contact) => CallComMethodWithArgs(() => _comObject.SetCustomerContact(contact), "SetCustomerContact");
        public int RefundReceiptInfo(string eCR, string receiptNo, string docNo) => CallComMethodWithArgs(() => _comObject.RefundReceiptInfo(eCR, receiptNo, docNo), "RefundReceiptInfo");
        public int GoodsReturnCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => CallComMethodWithArgs(() => _comObject.GoodsReturnCurr(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), "GoodsReturnCurr");
        public int EndFiscalReceiptEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => CallComMethodWithArgs(() => _comObject.EndFiscalReceiptEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), "EndFiscalReceiptEx");
        public int GoodsReturnEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => CallComMethodWithArgs(() => _comObject.GoodsReturnEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), "GoodsReturnEx");
        public int EndRecPaymentEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => CallComMethodWithArgs(() => _comObject.EndRecPaymentEx(rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8), "EndRecPaymentEx");
        public int EndFiscalCacheReceipt() => CallComMethod(() => _comObject.EndFiscalCacheReceipt(), "EndFiscalCacheReceipt");
        public int GoodsReturnCacheReceipt() => CallComMethod(() => _comObject.GoodsReturnCacheReceipt(), "GoodsReturnCacheReceipt");
        public int EndRecPayment(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => CallComMethodWithArgs(() => _comObject.EndRecPayment(rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3), "EndRecPayment");
        public int PrintCopyOfLastReceipt() => CallComMethod(() => _comObject.PrintCopyOfLastReceipt(), "PrintCopyOfLastReceipt");
        public int PrintCopyOfReceipt(int from, int to) => CallComMethodWithArgs(() => _comObject.PrintCopyOfReceipt(from, to), "PrintCopyOfReceipt");
        public int SetFooter(string line1, string line2, string line3, string line4) => CallComMethodWithArgs(() => _comObject.SetFooter(64, line1, 64, line2, 64, line3, 64, line4), "SetFooter");

        // This method handles the ref parameter and cannot use the generic wrapper
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
                var task = Task.Run(() => {
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
            _logger.Info("Reloading the Empirija COM object.");
            try
            {
                if (_comObject != null)
                {
                    Marshal.ReleaseComObject(_comObject);
                    _comObject = null;
                }
            }
            finally
            {
                InitializeComObject();
            }
        }

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