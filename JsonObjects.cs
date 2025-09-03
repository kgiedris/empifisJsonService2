using System;
using System.Collections.Generic;

namespace empifisJsonAPI2.JsonObjects
{
    public class fiscalCommand
    {
        public string Command { get; set; }
        public PrintCommentLine PrintCommentLine { get; set; }
        public SetFooter SetFooter { get; set; }
        public PrintRecItem PrintRecItem { get; set; }
        public PrintRecItemEx PrintRecItemEx { get; set; }
        public ItemReturn ItemReturn { get; set; }
        public ItemReturnEx ItemReturnEx { get; set; }
        public PrintDepositReceive PrintDepositReceive { get; set; }
        public EndFiscalReceiptCurr EndFiscalReceiptCurr { get; set; }
        public EndRecPayment EndRecPayment { get; set; }
        public EndFiscalReceiptEx EndFiscalReceiptEx { get; set; }
        public GoodsReturnEx GoodsReturnEx { get; set; }
        public EndRecPaymentEx EndRecPaymentEx { get; set; }
        public GetFiscalInfoParams GetFiscalInfo { get; set; }
        public PrintCopyOfReceipt PrintCopyOfReceipt { get; set; }
        public MoneyInCurr MoneyInCurr { get; set; }
        public MoneyOutCurr MoneyOutCurr { get; set; }
        public DiscountAdditionForItem DiscountAdditionForItem { get; set; }
        public DiscountAdditionForReceipt DiscountAdditionForReceipt { get; set; }
        public TransferPreReceipt TransferPreReceipt { get; set; }
        public LinkPreReceipt LinkPreReceipt { get; set; }
        public SetCustomerContact SetCustomerContact { get; set; }
        public RefundReceiptInfo RefundReceiptInfo { get; set; }
        public GoodsReturnCurr GoodsReturnCurr { get; set; }
        public Report Report { get; set; }
        public PrintSumPeriodicReport PrintSumPeriodicReport { get; set; }
        public PrintPeriodicReport PrintPeriodicReport { get; set; }
        public PrintSumPeriodicReportByNumber PrintSumPeriodicReportByNumber { get; set; }
        public PrintPeriodicReportByNumber PrintPeriodicReportByNumber { get; set; }
        public CustomerDisplay2 CustomerDisplay2 { get; set; }
        public CustomerDisplayPro CustomerDisplayPro { get; set; }
        public PrintTareItem PrintTareItem { get; set; }
        public PrintTareItemVoid PrintTareItemVoid { get; set; }
        public PrintDepositReceiveCredit PrintDepositReceiveCredit { get; set; }
        public PrintDepositReceiveVoid PrintDepositReceiveVoid { get; set; }
        public PrintDepositRefund PrintDepositRefund { get; set; }
        public PrintBarCode PrintBarCode { get; set; }
        public PrintNonFiscalLine PrintNonFiscalLine { get; set; }
    }

    public class ReceiptJson
    {
        public string ReceiptType { get; set; }
        public FiscalReceipt FiscalReceipt { get; set; }
        public NonFiscalReceipt NonFiscalReceipt { get; set; }
        public ResetFiscal ResetFiscal { get; set; }
        public Report Report { get; set; }
        public ReturnReceipt ReturnReceipt { get; set; }
        public SpecialFunction SpecialFunction { get; set; }
        public GetFiscalInfoParams GetFiscalInfo { get; set; }
        public List<CommentLines> TopCommentLines { get; set; }
        public List<CommentLines> BottomCommentLines { get; set; }
        public SetFooter SetFooter { get; set; }

        public ReceiptJson()
        {
            this.ReceiptType = string.Empty;
            this.FiscalReceipt = new FiscalReceipt();
            this.NonFiscalReceipt = new NonFiscalReceipt();
            this.Report = new Report();
            this.SpecialFunction = new SpecialFunction();
            this.GetFiscalInfo = new GetFiscalInfoParams();
            this.TopCommentLines = new List<CommentLines>();
            this.BottomCommentLines = new List<CommentLines>();
            this.SetFooter = new SetFooter();
        }
    }

    public class FiscalReceipt
    {
        public List<ReceiptItems> ReceiptItem { get; set; }
        public List<LinkPreReceipts> LinkPreReceipt { get; set; }
        public List<DepositReceive> DepositReceive { get; set; }
        public ReceiptPayment ReceiptPayment { get; set; }
        public ReceiptPaymentEx ReceiptPaymentEx { get; set; }
        public ReceiptDiscount ReceiptDiscount { get; set; }
        public EndPreReceipt EndPreReceipt { get; set; }
        public SetFooter SetFooter { get; set; }

        public FiscalReceipt()
        {
            this.ReceiptPayment = new ReceiptPayment();
            this.ReceiptPaymentEx = new ReceiptPaymentEx();
            this.ReceiptDiscount = new ReceiptDiscount();
            this.EndPreReceipt = new EndPreReceipt();
            this.SetFooter = new SetFooter();
            this.ReceiptItem = new List<ReceiptItems>();
            this.DepositReceive = new List<DepositReceive>();
            this.LinkPreReceipt = new List<LinkPreReceipts>();
        }
    }

    public class ReturnReceipt
    {
        public List<ReceiptItems> ReceiptItem { get; set; }
        public GoodsReturnPayment GoodsReturnPayment { get; set; }
        public GoodsReturnPaymentEx GoodsReturnPaymentEx { get; set; }
        public ReceiptDiscount ReceiptDiscount { get; set; }
        public RefundReceiptInfo RefundReceiptInfo { get; set; }
        public ReturnReceiptInfo ReturnReceiptInfo { get; set; }

        public ReturnReceipt()
        {
            this.GoodsReturnPayment = new GoodsReturnPayment();
            this.GoodsReturnPaymentEx = new GoodsReturnPaymentEx();
            this.ReceiptDiscount = new ReceiptDiscount();
            this.RefundReceiptInfo = new RefundReceiptInfo();
            this.ReturnReceiptInfo = new ReturnReceiptInfo();
            this.ReceiptItem = new List<ReceiptItems>();
        }
    }

    public class PrintCommentLine
    {
        public string CommentLine { get; set; }
        public int CommentLineAttrib { get; set; }
    }

    public class EndPreReceipt
    {
        public string EndPreReceiptLine { get; set; } = string.Empty;
    }

    public class SetCustomerContact
    {
        public string Contact { get; set; }
    }

    public class GoodsReturnPayment
    {
        public double Cash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
    }

    public class GoodsReturnPaymentEx
    {
        public double Cash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double Credit5 { get; set; }
        public double Credit6 { get; set; }
        public double Credit7 { get; set; }
        public double Credit8 { get; set; }
    }

    public class ReceiptItems
    {
        public string ItemDescription { get; set; }
        public double ItemQuantity { get; set; }
        public double ItemPrice { get; set; }
        public int VatID { get; set; }
        public string ItemUnit { get; set; } = "vnt";
        public string ItemGroup { get; set; } = "GR";
        public ItemDiscount ItemDiscount { get; set; }
        public List<CommentLines> CommentLines { get; set; }

        public ReceiptItems()
        {
            this.ItemDescription = string.Empty;
            this.ItemUnit = "vnt";
            this.ItemGroup = "GR";
            this.ItemDiscount = new ItemDiscount();
            this.CommentLines = new List<CommentLines>();
        }
    }

    public class PrintRecItem
    {
        public string ItemDescription { get; set; }
        public double ItemQuantity { get; set; }
        public double ItemPrice { get; set; }
        public int VatID { get; set; }
        public string ItemUnit { get; set; }
    }

    public class PrintTareItem
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
    }

    public class PrintTareItemVoid
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
    }

    public class PrintDepositReceive
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
    }

    public class PrintDepositReceiveCredit
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
    }

    public class DiscountAdditionForItem
    {
        public int Type { get; set; }
        public double Amount { get; set; }
    }

    public class DiscountAdditionForReceipt
    {
        public int Type { get; set; }
        public double Amount { get; set; }
    }

    public class TransferPreReceipt
    {
        public string ReceiptNo { get; set; }
        public double Amount { get; set; }
    }

    public class LinkPreReceipt
    {
        public string ReceiptNo { get; set; }
        public double Amount { get; set; }
    }

    public class LinkPreReceipts
    {
        public string ReceiptNo { get; set; }
        public double Amount { get; set; }
    }

    public class RefundReceiptInfo
    {
        public string ECR { get; set; }
        public string ReceiptNo { get; set; }
        public string DocNo { get; set; }
    }

    public class ReturnReceiptInfo
    {
        public string ECR { get; set; }
        public string ReceiptNo { get; set; }
        public string DocNo { get; set; }
    }

    public class PrintDepositRefund
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
    }

    public class PrintDepositReceiveVoid
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
    }

    public class PrintBarCode
    {
        public int System { get; set; }
        public int Height { get; set; }
        public string BarCode { get; set; }
    }

    public class PrintRecItemEx
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
        public int Vat { get; set; }
        public string Dimension { get; set; }
        public string Group { get; set; }
    }

    public class ItemReturnEx
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
        public int Vat { get; set; }
        public string Dimension { get; set; }
        public string Group { get; set; }
        public double CurrPercent { get; set; }
        public double CurrAbsolute { get; set; }
    }

    public class ItemReturn
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public double Price { get; set; }
        public int Vat { get; set; }
        public string Dimension { get; set; }
        public double CurrPercent { get; set; }
        public double CurrAbsolute { get; set; }
    }

    public class PrintNonFiscalLine
    {
        public string Line { get; set; }
        public int Atrrib { get; set; }
    }

    public class CommentLines
    {
        public string CommentLine { get; set; }
        public int CommentLineAttrib { get; set; }

        public CommentLines()
        {
            this.CommentLine = string.Empty;
            this.CommentLineAttrib = 64;
        }
    }

    public class SetFooter
    {
        public string line1 { get; set; }
        public string line2 { get; set; }
        public string line3 { get; set; }
        public string line4 { get; set; }
        public int CommentLineAttrib { get; set; }
    }

    public class ReceiptPayment
    {
        public double Cash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
    }

    public class ReceiptPaymentEx
    {
        public double Cash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double Credit5 { get; set; }
        public double Credit6 { get; set; }
        public double Credit7 { get; set; }
        public double Credit8 { get; set; }
    }

    public class EndFiscalReceiptCurr
    {
        public double rCash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double rCurrency1 { get; set; }
        public double rCurrency2 { get; set; }
        public double rCurrency3 { get; set; }
    }

    public class EndRecPayment
    {
        public double rCash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double rCurrency1 { get; set; }
        public double rCurrency2 { get; set; }
        public double rCurrency3 { get; set; }
    }

    public class GoodsReturnCurr
    {
        public double rCash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double rCurrency1 { get; set; }
        public double rCurrency2 { get; set; }
        public double rCurrency3 { get; set; }
    }

    public class EndFiscalReceiptEx
    {
        public double rCash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double Credit5 { get; set; }
        public double Credit6 { get; set; }
        public double Credit7 { get; set; }
        public double Credit8 { get; set; }
    }

    public class GoodsReturnEx
    {
        public double rCash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double Credit5 { get; set; }
        public double Credit6 { get; set; }
        public double Credit7 { get; set; }
        public double Credit8 { get; set; }
    }

    public class EndRecPaymentEx
    {
        public double rCash { get; set; }
        public double Credit1 { get; set; }
        public double Credit2 { get; set; }
        public double Credit3 { get; set; }
        public double Credit4 { get; set; }
        public double Credit5 { get; set; }
        public double Credit6 { get; set; }
        public double Credit7 { get; set; }
        public double Credit8 { get; set; }
    }

    public class ItemDiscount
    {
        public int ItemDiscountType { get; set; }
        public double ItemDiscountAmount { get; set; }
    }

    public class ReceiptDiscount
    {
        public int ReceiptDiscountType { get; set; }
        public double ReceiptDiscountAmount { get; set; }
    }

    public class NonFiscalReceipt
    {
        public List<Tare> Tare { get; set; }
        public List<DepositReceive> DepositReceive { get; set; }
        public List<DepositReceiveCredit> DepositReceiveCredit { get; set; }
        public List<DepositRefund> DepositRefund { get; set; }

        public NonFiscalReceipt()
        {
            this.Tare = new List<Tare>();
            this.DepositReceive = new List<DepositReceive>();
            this.DepositReceiveCredit = new List<DepositReceiveCredit>();
            this.DepositRefund = new List<DepositRefund>();
        }
    }

    public class Tare
    {
        public string TareDescription { get; set; }
        public double TareQuantity { get; set; }
        public double TarePrice { get; set; }
        public List<CommentLines> CommentLines { get; set; }

        public Tare()
        {
            this.TareDescription = string.Empty;
            this.CommentLines = new List<CommentLines>();
        }
    }

    public class DepositReceive
    {
        public string DepositReceiveDesc { get; set; }
        public double DepositReceiveQ { get; set; }
        public double DepositReceivePrice { get; set; }
        public List<CommentLines> CommentLines { get; set; }

        public DepositReceive()
        {
            this.DepositReceiveDesc = string.Empty;
            this.CommentLines = new List<CommentLines>();
        }
    }

    public class DepositReceiveCredit
    {
        public string depositReceiveCreditDesc { get; set; }
        public double DepositReceiveCreditQ { get; set; }
        public double DepositReceiveCreditPrice { get; set; }
        public List<CommentLines> CommentLines { get; set; }

        public DepositReceiveCredit()
        {
            this.depositReceiveCreditDesc = string.Empty;
            this.CommentLines = new List<CommentLines>();
        }
    }

    public class DepositRefund
    {
        public string DepositRefundDescription { get; set; }
        public double DepositRefundQuantity { get; set; }
        public double DepositRefundPrice { get; set; }
        public List<CommentLines> CommentLines { get; set; }

        public DepositRefund()
        {
            this.DepositRefundDescription = string.Empty;
            this.CommentLines = new List<CommentLines>();
        }
    }

    public class MoneyInCurr
    {
        public double Amount { get; set; }
    }

    public class PrintCopyOfReceipt
    {
        public int From { get; set; }
        public int To { get; set; }
    }

    public class MoneyOutCurr
    {
        public double Amount { get; set; }
    }

    public class PrintSumPeriodicReport
    {
        public string dateFrom { get; set; }
        public string dateTo { get; set; }
    }

    public class CustomerDisplay2
    {
        public string Line1 { get; set; }
        public string Line2 { get; set; }
    }

    public class CustomerDisplayPro
    {
        public string Line { get; set; }
    }

    public class PrintPeriodicReport
    {
        public string dateFrom { get; set; }
        public string dateTo { get; set; }
    }

    public class PrintSumPeriodicReportByNumber
    {
        public int noFrom { get; set; }
        public int noTo { get; set; }
    }

    public class PrintPeriodicReportByNumber
    {
        public int noFrom { get; set; }
        public int noTo { get; set; }
    }

    public class SpecialFunction
    {
        public string Function { get; set; }
        public double Amount { get; set; }
        public string RecNo { get; set; }

        public SpecialFunction()
        {
            this.Function = string.Empty;
            this.RecNo = string.Empty;
        }
    }

    public class GetFiscalInfoParams
    {
        public int InfoType { get; set; }
    }

    public class ResetFiscal
    {
        public string reset { get; set; }
    }

    public class Report
    {
        public string ReportType { get; set; }
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public int NoFrom { get; set; }
        public int NoTo { get; set; }
    }

    public class ResponseJson
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        public ResponseJson()
        {
            this.ErrorMessage = string.Empty;
        }
    }

    public class ResponseJsonRadison
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string CashRegisterNo { get; set; }
        public string ReceiptNo { get; set; }

        public ResponseJsonRadison()
        {
            this.ErrorMessage = string.Empty;
            this.CashRegisterNo = string.Empty;
            this.ReceiptNo = string.Empty;
        }
    }

    public class Configuration
    {
        public string InFilePatch { get; set; }
        public string InFileArchivePath { get; set; }
        public string OutFilePath { get; set; }
        public string OutFileArchivePath { get; set; }
    }
}