using NLog;
using empifisJsonAPI2.JsonObjects;
using System.Collections.Generic;
using System.Linq;
using System;

namespace empifisJsonAPI2
{
    public class ReceiptProcessor
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly IFiscalDevice _comManager;

        public ReceiptProcessor(IFiscalDevice comManager)
        {
            _comManager = comManager;
        }

        public (int errorCode, string message) ProcessReceipt(ReceiptJson jsonReceipt)
        {
            if (jsonReceipt == null || string.IsNullOrEmpty(jsonReceipt.ReceiptType))
            {
                _logger.Warn("Received a null or invalid JSON receipt.");
                return (999, "Invalid JSON receipt.");
            }

            // Declared here to be visible across the entire function
            int errorCode = 0;
            string message = "";

            // For receipts that must never print twice, remember the device's next receipt number so a
            // failure (especially a COM timeout) can be checked against what the device actually did.
            string receiptType = jsonReceipt.ReceiptType.ToLower();
            long? receiptNoBefore = receiptType == "fiscal" || receiptType == "return" ? ReadNextReceiptNo() : null;

            try
            {
                switch (receiptType)
                {
                    case "fiscal":
                        errorCode = ProcessFiscalReceipt(jsonReceipt.FiscalReceipt, jsonReceipt);
                        break;
                    case "nonfiscal":
                        // If incoming JSON omitted NonFiscalReceipt, provide an empty default only for non-fiscal receipts
                        jsonReceipt.NonFiscalReceipt ??= new NonFiscalReceipt();
                        errorCode = ProcessNonFiscalReceipt(jsonReceipt.NonFiscalReceipt, jsonReceipt);
                        break;
                    case "return":
                        errorCode = ProcessReturnReceipt(jsonReceipt.ReturnReceipt, jsonReceipt);
                        break;
                    case "report":
                        errorCode = ProcessReport(jsonReceipt.Report);
                        break;
                    case "special":
                        errorCode = ProcessSpecialFunction(jsonReceipt.SpecialFunction);
                        break;

                    case "getfiscalinfo":
                        if (jsonReceipt.GetFiscalInfo != null)
                        {
                            var infoResult = _comManager.GetFiscalInfo(jsonReceipt.GetFiscalInfo.InfoType);

                            errorCode = infoResult.errorCode;
                            message = infoResult.message; // CAPTURES FISCAL DATA

                            _logger.Info($"Fiscal Info Result: {message}");
                        }
                        else
                        {
                            _logger.Warn("Missing 'getFiscalInfo' object for 'getfiscalinfo' command.");
                            errorCode = 999;
                            message = "Missing 'getFiscalInfo' object.";
                        }
                        return (errorCode, message); // Returns early to preserve the custom message

                    case "reset":
                        errorCode = _comManager.ResetFiscal();
                        break;
                    default:
                        _logger.Warn($"Invalid or unsupported ReceiptType: {jsonReceipt.ReceiptType}");
                        errorCode = 999;
                        message = $"Invalid or unsupported ReceiptType: {jsonReceipt.ReceiptType}";
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An exception occurred while processing a receipt.");
                errorCode = 999;
                message = ex.Message; // Captures exception message
            }

            // The device may have completed the receipt even though we got an error (e.g. the final
            // End* call timed out but the printer finished). If its receipt counter moved, the receipt
            // exists: report success and don't reset, otherwise the POS retries and prints a duplicate.
            if (errorCode != 0 && receiptNoBefore.HasValue && ReadNextReceiptNo() is long receiptNoAfter && receiptNoAfter > receiptNoBefore.Value)
            {
                _logger.Warn($"Receipt reported error {errorCode} ('{message}') but the device's next receipt number moved from {receiptNoBefore} to {receiptNoAfter}. Treating the receipt as completed.");
                return (0, "Success");
            }

            // Final checks before returning the standard receipt result
            if (errorCode != 0)
            {
                _comManager.ResetFiscal();
                // If message is empty (i.e., not set by the catch block), provide a generic error
                if (string.IsNullOrEmpty(message))
                {
                    message = ErrorCodes.Describe(errorCode);
                }
            }
            else if (string.IsNullOrEmpty(message))
            {
                // Set default success message if the execution was clean
                message = "Success";
            }

            return (errorCode, message);
        }

        // GetFiscalInfo type 2 = "Total number of next receipt (document)". Null if it can't be read.
        private long? ReadNextReceiptNo()
        {
            var (code, info) = _comManager.GetFiscalInfo(2);
            return code == 0 && long.TryParse(info?.Trim(), out var receiptNo) ? receiptNo : null;
        }

        private int ProcessFiscalReceipt(FiscalReceipt fiscalReceipt, ReceiptJson jsonReceipt)
        {
            if (fiscalReceipt == null) return 999;

            int errorCode;
            if ((errorCode = Logged(_comManager.BeginFiscalReceipt(), "BeginFiscalReceipt")) != 0) return errorCode;
            if ((errorCode = PrintLines(jsonReceipt.TopCommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = PrintItems(fiscalReceipt.ReceiptItem, PrintSaleItem, i => i.CommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = PrintItems(fiscalReceipt.DepositReceive, PrintDepositReceive, i => i.CommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = PrintItems(fiscalReceipt.PrintTareDeposit, PrintTareDeposit, i => i.CommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = PrintItems(fiscalReceipt.PrintTareDepositVoid, PrintTareDepositVoid, i => i.CommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = PrintItems(fiscalReceipt.LinkPreReceipt, link => Logged(_comManager.LinkPreReceipt(link.ReceiptNo, link.Amount),
                $"LinkPreReceipt with params ('{link.ReceiptNo}', {link.Amount})"), _ => null, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = ApplyReceiptDiscount(fiscalReceipt.ReceiptDiscount)) != 0) return errorCode;
            if ((errorCode = PrintLines(jsonReceipt.BottomCommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = SetFooter(jsonReceipt.SetFooter)) != 0) return errorCode;

            if (fiscalReceipt.EndPreReceipt?.EndPreReceiptLine == "EndPreReceipt")
            {
                return Logged(_comManager.EndPreReceipt(), "EndPreReceipt");
            }
            // An explicit payment with a zero total is treated like no payment (accepted behaviour).
            PaymentAmounts? payment = (fiscalReceipt.ReceiptPaymentEx?.Sum() ?? 0) > 0 ? fiscalReceipt.ReceiptPaymentEx
                : (fiscalReceipt.ReceiptPayment?.Sum() ?? 0) > 0 ? fiscalReceipt.ReceiptPayment
                : null;
            if (payment != null)
            {
                return Logged(_comManager.EndFiscalReceiptEx(payment.Cash, payment.Credit1, payment.Credit2, payment.Credit3,
                    payment.Credit4, payment.Credit5, payment.Credit6, payment.Credit7, payment.Credit8),
                    $"EndFiscalReceiptEx with params ({payment.Cash}, {payment.Credit1}, {payment.Credit2}, {payment.Credit3}, {payment.Credit4}, {payment.Credit5}, {payment.Credit6}, {payment.Credit7}, {payment.Credit8})");
            }
            return Logged(_comManager.EndFiscalCacheReceipt(), "EndFiscalCacheReceipt (no payment specified)");
        }

        private int ProcessNonFiscalReceipt(NonFiscalReceipt nonFiscalReceipt, ReceiptJson jsonReceipt)
        {
            if (nonFiscalReceipt == null) return 999;

            int errorCode;
            if ((errorCode = Logged(_comManager.BeginNonFiscalReceipt(), "BeginNonFiscalReceipt")) != 0) return errorCode;
            if ((errorCode = PrintLines(jsonReceipt.TopCommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintItems(nonFiscalReceipt.Tare, item => Logged(_comManager.PrintTareItem(item.TareDescription, item.TareQuantity, item.TarePrice),
                $"PrintTareItem with params ('{item.TareDescription}', {item.TareQuantity}, {item.TarePrice})"), i => i.CommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintItems(nonFiscalReceipt.DepositReceive, PrintDepositReceive, i => i.CommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintItems(nonFiscalReceipt.PrintTareDeposit, PrintTareDeposit, i => i.CommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintItems(nonFiscalReceipt.PrintTareDepositVoid, PrintTareDepositVoid, i => i.CommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintItems(nonFiscalReceipt.DepositReceiveCredit, item => Logged(_comManager.PrintDepositReceiveCredit(item.depositReceiveCreditDesc, item.DepositReceiveCreditQ, item.DepositReceiveCreditPrice),
                $"PrintDepositReceiveCredit with params ('{item.depositReceiveCreditDesc}', {item.DepositReceiveCreditQ}, {item.DepositReceiveCreditPrice})"), i => i.CommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintItems(nonFiscalReceipt.DepositRefund, item => Logged(_comManager.PrintDepositRefund(item.DepositRefundDescription, item.DepositRefundQuantity, item.DepositRefundPrice),
                $"PrintDepositRefund with params ('{item.DepositRefundDescription}', {item.DepositRefundQuantity}, {item.DepositRefundPrice})"), i => i.CommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = PrintLines(jsonReceipt.BottomCommentLines, nonFiscal: true)) != 0) return errorCode;
            if ((errorCode = SetFooter(jsonReceipt.SetFooter)) != 0) return errorCode;

            return Logged(_comManager.EndNonFiscalReceipt(), "EndNonFiscalReceipt");
        }

        private int ProcessReturnReceipt(ReturnReceipt returnReceipt, ReceiptJson jsonReceipt)
        {
            if (returnReceipt == null || returnReceipt.ReceiptItem == null)
            {
                _logger.Warn("ReceiptItem property is null in a return receipt.");
                return 999;
            }

            int errorCode;
            if ((errorCode = Logged(_comManager.BeginFiscalReceipt(), "BeginFiscalReceipt")) != 0) return errorCode;
            if ((errorCode = PrintLines(jsonReceipt.TopCommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = PrintItems(returnReceipt.ReceiptItem, PrintSaleItem, i => i.CommentLines, nonFiscal: false)) != 0) return errorCode;
            if ((errorCode = ApplyReceiptDiscount(returnReceipt.ReceiptDiscount)) != 0) return errorCode;
            if ((errorCode = PrintLines(jsonReceipt.BottomCommentLines, nonFiscal: false)) != 0) return errorCode;

            // The manual names this object RefundReceiptInfo in the example and ReturnReceiptInfo in the
            // table; accept either, but send it to the device only once.
            if (returnReceipt.RefundReceiptInfo != null && returnReceipt.ReturnReceiptInfo != null)
            {
                _logger.Warn("Both RefundReceiptInfo and ReturnReceiptInfo were given; using RefundReceiptInfo.");
            }
            RefundReceiptInfo? refundInfo = returnReceipt.RefundReceiptInfo ?? returnReceipt.ReturnReceiptInfo;
            if (refundInfo != null && (errorCode = Logged(_comManager.RefundReceiptInfo(refundInfo.ECR, refundInfo.ReceiptNo, refundInfo.DocumentNumber),
                $"RefundReceiptInfo with params ('{refundInfo.ECR}', '{refundInfo.ReceiptNo}', '{refundInfo.DocumentNumber}')")) != 0) return errorCode;

            if ((errorCode = SetFooter(jsonReceipt.SetFooter)) != 0) return errorCode;

            if ((returnReceipt.GoodsReturnPaymentEx?.Sum() ?? 0) > 0)
            {
                var p = returnReceipt.GoodsReturnPaymentEx!;
                return Logged(_comManager.GoodsReturnEx(p.Cash, p.Credit1, p.Credit2, p.Credit3, p.Credit4, p.Credit5, p.Credit6, p.Credit7, p.Credit8),
                    $"GoodsReturnEx with params ({p.Cash}, {p.Credit1}, {p.Credit2}, {p.Credit3}, {p.Credit4}, {p.Credit5}, {p.Credit6}, {p.Credit7}, {p.Credit8})");
            }
            if ((returnReceipt.GoodsReturnPayment?.Sum() ?? 0) > 0)
            {
                var p = returnReceipt.GoodsReturnPayment!;
                return Logged(_comManager.GoodsReturnCurr(p.Cash, p.Credit1, p.Credit2, p.Credit3, p.Credit4, 0, 0, 0),
                    $"GoodsReturnCurr with params ({p.Cash}, {p.Credit1}, {p.Credit2}, {p.Credit3}, {p.Credit4}, 0, 0, 0)");
            }
            return Logged(_comManager.GoodsReturnCacheReceipt(), "GoodsReturnCacheReceipt (no payment specified)");
        }

        private int ProcessReport(Report report)
        {
            if (report == null || string.IsNullOrEmpty(report.ReportType)) return 999;

            switch (report.ReportType.ToLower())
            {
                case "minix":
                    return Logged(_comManager.PrintMiniXReport(), "PrintMiniXReport");
                case "printx":
                    return Logged(_comManager.PrintXReport(), "PrintXReport");
                case "printz":
                    return Logged(_comManager.PrintZReport(), "PrintZReport");
                case "sumperiodic":
                    return Logged(_comManager.PrintSumPeriodicReport(report.DateFrom, report.DateTo), $"PrintSumPeriodicReport with params ('{report.DateFrom}', '{report.DateTo}')");
                case "periodic":
                    return Logged(_comManager.PrintPeriodicReport(report.DateFrom, report.DateTo), $"PrintPeriodicReport with params ('{report.DateFrom}', '{report.DateTo}')");
                case "sumperiodicbynumber":
                    return Logged(_comManager.PrintSumPeriodicReportByNumber(report.NoFrom, report.NoTo), $"PrintSumPeriodicReportByNumber with params ({report.NoFrom}, {report.NoTo})");
                case "periodicbynumber":
                    return Logged(_comManager.PrintPeriodicReportByNumber(report.NoFrom, report.NoTo), $"PrintPeriodicReportByNumber with params ({report.NoFrom}, {report.NoTo})");
                default:
                    _logger.Warn($"Invalid reportType: {report.ReportType}");
                    return 999;
            }
        }

        private int ProcessSpecialFunction(SpecialFunction specialFunction)
        {
            if (specialFunction == null || string.IsNullOrEmpty(specialFunction.Function)) return 999;

            switch (specialFunction.Function.ToLower())
            {
                case "moneyin":
                case "moneyincurr":
                    return Logged(_comManager.MoneyInCurr(0, specialFunction.Amount), $"MoneyInCurr with params (0, {specialFunction.Amount})");
                case "moneyout":
                case "moneyoutcurr":
                    return Logged(_comManager.MoneyOutCurr(0, specialFunction.Amount), $"MoneyOutCurr with params (0, {specialFunction.Amount})");
                case "transferprereceipt":
                    return Logged(_comManager.TransferPreReceipt(specialFunction.RecNo, specialFunction.Amount), $"TransferPreReceipt with params ('{specialFunction.RecNo}', {specialFunction.Amount})");
                case "opencashdrawer":
                    return Logged(_comManager.OpenCashDrawer(), "OpenCashDrawer");
                default:
                    _logger.Warn($"Invalid special function: {specialFunction.Function}");
                    return 999;
            }
        }

        // Logs a device call with its result and returns the result.
        private int Logged(int errorCode, string call)
        {
            _logger.Debug($"Called {call}. Response: {errorCode}");
            return errorCode;
        }

        // Comment lines: PrintCommentLine inside a fiscal or return receipt, PrintNonFiscalLine inside a
        // non-fiscal one. Empty lines are skipped. Returns the first error, or 0.
        private int PrintLines(IEnumerable<CommentLines>? lines, bool nonFiscal)
        {
            foreach (var line in (lines ?? Enumerable.Empty<CommentLines>()).Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
            {
                int errorCode = nonFiscal
                    ? Logged(_comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib), $"PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib})")
                    : Logged(_comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib), $"PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib})");
                if (errorCode != 0) return errorCode;
            }
            return 0;
        }

        // Prints each item followed by its own comment lines. Returns the first error, or 0.
        private int PrintItems<T>(IEnumerable<T>? items, Func<T, int> printItem, Func<T, IEnumerable<CommentLines>?> commentLines, bool nonFiscal)
        {
            foreach (var item in (items ?? Enumerable.Empty<T>()).Where(i => i != null))
            {
                int errorCode = printItem(item);
                if (errorCode != 0) return errorCode;
                if ((errorCode = PrintLines(commentLines(item), nonFiscal)) != 0) return errorCode;
            }
            return 0;
        }

        // A sold (or, in a return receipt, returned) item and its optional discount/surcharge.
        private int PrintSaleItem(ReceiptItems item)
        {
            int errorCode = Logged(_comManager.PrintRecItemEx(item.ItemDescription, item.ItemQuantity, item.ItemPrice, item.VatID, item.ItemUnit, item.ItemGroup),
                $"PrintRecItemEx with params ('{item.ItemDescription}', {item.ItemQuantity}, {item.ItemPrice}, {item.VatID}, '{item.ItemUnit}', '{item.ItemGroup}')");
            if (errorCode != 0 || item.ItemDiscount == null || item.ItemDiscount.ItemDiscountType == 999) return errorCode;
            return Logged(_comManager.DiscountAdditionForItem(item.ItemDiscount.ItemDiscountType, item.ItemDiscount.ItemDiscountAmount),
                $"DiscountAdditionForItem with params ({item.ItemDiscount.ItemDiscountType}, {item.ItemDiscount.ItemDiscountAmount})");
        }

        private int PrintDepositReceive(DepositReceive item) =>
            Logged(_comManager.PrintDepositReceive(item.DepositReceiveDesc, item.DepositReceiveQ, item.DepositReceivePrice),
                $"PrintDepositReceive with params ('{item.DepositReceiveDesc}', {item.DepositReceiveQ}, {item.DepositReceivePrice})");

        private int PrintTareDeposit(PrintTareDeposit item) =>
            Logged(_comManager.PrintTareDeposit(item.Description, item.Quantity, item.UnitPrice),
                $"PrintTareDeposit with params ('{item.Description}', {item.Quantity}, {item.UnitPrice})");

        private int PrintTareDepositVoid(PrintTareDepositVoid item) =>
            Logged(_comManager.PrintTareDepositVoid(item.Description, item.Quantity, item.UnitPrice),
                $"PrintTareDepositVoid with params ('{item.Description}', {item.Quantity}, {item.UnitPrice})");

        private int ApplyReceiptDiscount(ReceiptDiscount? discount)
        {
            if (discount == null || discount.ReceiptDiscountType == 999) return 0;
            return Logged(_comManager.DiscountAdditionForReceipt(discount.ReceiptDiscountType, discount.ReceiptDiscountAmount),
                $"DiscountAdditionForReceipt with params ({discount.ReceiptDiscountType}, {discount.ReceiptDiscountAmount})");
        }

        private int SetFooter(SetFooter? footer)
        {
            if (footer == null) return 0;
            return Logged(_comManager.SetFooter(footer.line1, footer.line2, footer.line3, footer.line4), $"SetFooter with params ('{footer.line1}', etc)");
        }
    }
}
