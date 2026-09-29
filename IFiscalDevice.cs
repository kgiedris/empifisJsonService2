using System;
using System.Threading.Tasks;

namespace empifisJsonAPI2
{
    /// <summary>
    /// The fiscal device operations used by ReceiptProcessor and Worker. Implemented by
    /// EmpifisComManager; tests substitute a fake so receipt logic can run without hardware.
    /// </summary>
    public interface IFiscalDevice
    {
        Task<IDisposable> AcquireDeviceLockAsync();
        (int errorCode, string message) GetFiscalInfo(int infoType);
        int ResetFiscal();

        int BeginFiscalReceipt();
        int PrintCommentLine(string commentLine, int commentLineAttrib);
        int PrintRecItemEx(string description, double quantity, double price, int vat, string dimension, string group);
        int DiscountAdditionForItem(int type, double amount);
        int DiscountAdditionForReceipt(int type, double amount);
        int PrintDepositReceive(string description, double quantity, double price);
        int PrintTareDeposit(string description, double quantity, double unitPrice);
        int PrintTareDepositVoid(string description, double quantity, double unitPrice);
        int LinkPreReceipt(string receiptNo, double amount);
        int SetFooter(string line1, string line2, string line3, string line4);
        int EndPreReceipt();
        int EndFiscalReceiptEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8);
        int EndFiscalCacheReceipt();

        int RefundReceiptInfo(string eCR, string receiptNo, string docNo);
        int GoodsReturnEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8);
        int GoodsReturnCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3);
        int GoodsReturnCacheReceipt();

        int BeginNonFiscalReceipt();
        int PrintNonFiscalLine(string line, int attrib);
        int PrintTareItem(string description, double quantity, double price);
        int PrintDepositReceiveCredit(string description, double quantity, double price);
        int PrintDepositRefund(string description, double quantity, double price);
        int EndNonFiscalReceipt();

        int PrintMiniXReport();
        int PrintXReport();
        int PrintZReport();
        int PrintSumPeriodicReport(string dateFrom, string dateTo);
        int PrintPeriodicReport(string dateFrom, string dateTo);
        int PrintSumPeriodicReportByNumber(int noFrom, int noTo);
        int PrintPeriodicReportByNumber(int noFrom, int noTo);

        int MoneyInCurr(int paymentType, double amount);
        int MoneyOutCurr(int paymentType, double amount);
        int TransferPreReceipt(string receiptNo, double amount);
        int OpenCashDrawer();
    }
}
