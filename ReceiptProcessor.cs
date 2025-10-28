using NLog;
using empifisJsonAPI2.JsonObjects;
using System.Linq;
using System;

namespace empifisJsonAPI2
{
    public class ReceiptProcessor
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        private readonly EmpifisComManager _comManager;

        public ReceiptProcessor(EmpifisComManager comManager)
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

            try
            {
                switch (jsonReceipt.ReceiptType.ToLower())
                {
                    case "fiscal":
                        errorCode = ProcessFiscalReceipt(jsonReceipt.FiscalReceipt, jsonReceipt);
                        break;
                    case "nonfiscal":
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

            // Final checks before returning the standard receipt result
            if (errorCode != 0)
            {
                _comManager.ResetFiscal();
                // If message is empty (i.e., not set by the catch block), provide a generic error
                if (string.IsNullOrEmpty(message))
                {
                    message = "Error during receipt processing.";
                }
            }
            else if (string.IsNullOrEmpty(message))
            {
                // Set default success message if the execution was clean
                message = "Success";
            }

            return (errorCode, message);
        }

        private int ProcessFiscalReceipt(FiscalReceipt fiscalReceipt, ReceiptJson jsonReceipt)
        {
            if (fiscalReceipt == null) return 999;

            int errorCode = _comManager.BeginFiscalReceipt();
            _logger.Debug($"Called BeginFiscalReceipt. Response: {errorCode}");
            if (errorCode != 0) return errorCode;

            if (jsonReceipt.TopCommentLines != null)
            {
                foreach (var line in jsonReceipt.TopCommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                {
                    errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                    _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            if (fiscalReceipt.ReceiptItem != null)
            {
                foreach (var item in fiscalReceipt.ReceiptItem.Where(i => i != null))
                {
                    errorCode = _comManager.PrintRecItem(item.ItemDescription, item.ItemQuantity, item.ItemPrice, item.VatID, item.ItemUnit);
                    _logger.Debug($"Called PrintRecItem with params ('{item.ItemDescription}', {item.ItemQuantity}, {item.ItemPrice}, {item.VatID}, '{item.ItemUnit}'). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;

                    if (item.ItemDiscount != null && item.ItemDiscount.ItemDiscountType != 999)
                    {
                        errorCode = _comManager.DiscountAdditionForItem(item.ItemDiscount.ItemDiscountType, item.ItemDiscount.ItemDiscountAmount);
                        _logger.Debug($"Called DiscountAdditionForItem with params ({item.ItemDiscount.ItemDiscountType}, {item.ItemDiscount.ItemDiscountAmount}). Response: {errorCode}");
                        if (errorCode != 0) return errorCode;
                    }
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (fiscalReceipt.DepositReceive != null)
            {
                foreach (var item in fiscalReceipt.DepositReceive.Where(i => i != null))
                {
                    errorCode = _comManager.PrintDepositReceive(item.DepositReceiveDesc, item.DepositReceiveQ, item.DepositReceivePrice);
                    _logger.Debug($"Called PrintDepositReceive with params ('{item.DepositReceiveDesc}', {item.DepositReceiveQ}, {item.DepositReceivePrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (fiscalReceipt.PrintTareDeposit != null)
            {
                foreach (var item in fiscalReceipt.PrintTareDeposit.Where(i => i != null))
                {
                    errorCode = _comManager.PrintTareDeposit(item.Description, item.Quantity, item.UnitPrice);
                    _logger.Debug($"Called PrintTareDeposit with params ('{item.Description}', {item.Quantity}, {item.UnitPrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (fiscalReceipt.PrintTareDepositVoid != null)
            {
                foreach (var item in fiscalReceipt.PrintTareDepositVoid.Where(i => i != null))
                {
                    errorCode = _comManager.PrintTareDepositVoid(item.Description, item.Quantity, item.UnitPrice);
                    _logger.Debug($"Called PrintTareDepositVoid with params ('{item.Description}', {item.Quantity}, {item.UnitPrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (fiscalReceipt.LinkPreReceipt != null)
            {
                foreach (var link in fiscalReceipt.LinkPreReceipt.Where(l => l != null))
                {
                    errorCode = _comManager.LinkPreReceipt(link.ReceiptNo, link.Amount);
                    _logger.Debug($"Called LinkPreReceipt with params ('{link.ReceiptNo}', {link.Amount}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            if (fiscalReceipt.ReceiptDiscount != null && fiscalReceipt.ReceiptDiscount.ReceiptDiscountType != 999)
            {
                errorCode = _comManager.DiscountAdditionForReceipt(fiscalReceipt.ReceiptDiscount.ReceiptDiscountType, fiscalReceipt.ReceiptDiscount.ReceiptDiscountAmount);
                _logger.Debug($"Called DiscountAdditionForReceipt with params ({fiscalReceipt.ReceiptDiscount.ReceiptDiscountType}, {fiscalReceipt.ReceiptDiscount.ReceiptDiscountAmount}). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            if (jsonReceipt.BottomCommentLines != null)
            {
                foreach (var line in jsonReceipt.BottomCommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                {
                    errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                    _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            if (jsonReceipt.SetFooter != null)
            {
                errorCode = _comManager.SetFooter(jsonReceipt.SetFooter.line1, jsonReceipt.SetFooter.line2, jsonReceipt.SetFooter.line3, jsonReceipt.SetFooter.line4);
                _logger.Debug($"Called SetFooter with params ('{jsonReceipt.SetFooter.line1}', etc). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            bool isEndPreReceipt = jsonReceipt.FiscalReceipt?.EndPreReceipt?.EndPreReceiptLine == "EndPreReceipt";
            bool hasExtendedPayment = (jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Cash + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit1 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit2 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit3 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit4 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit5 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit6 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit7 + jsonReceipt.FiscalReceipt?.ReceiptPaymentEx?.Credit8) > 0;
            bool hasStandardPayment = (jsonReceipt.FiscalReceipt?.ReceiptPayment?.Cash + jsonReceipt.FiscalReceipt?.ReceiptPayment?.Credit1 + jsonReceipt.FiscalReceipt?.ReceiptPayment?.Credit2 + jsonReceipt.FiscalReceipt?.ReceiptPayment?.Credit3 + jsonReceipt.FiscalReceipt?.ReceiptPayment?.Credit4) > 0;

            if (isEndPreReceipt)
            {
                errorCode = _comManager.EndPreReceipt();
                _logger.Debug($"Called EndPreReceipt. Response: {errorCode}");
            }
            else if (hasExtendedPayment)
            {
                errorCode = _comManager.EndFiscalReceiptEx(
                    fiscalReceipt.ReceiptPaymentEx.Cash, fiscalReceipt.ReceiptPaymentEx.Credit1, fiscalReceipt.ReceiptPaymentEx.Credit2,
                    fiscalReceipt.ReceiptPaymentEx.Credit3, fiscalReceipt.ReceiptPaymentEx.Credit4, fiscalReceipt.ReceiptPaymentEx.Credit5,
                    fiscalReceipt.ReceiptPaymentEx.Credit6, fiscalReceipt.ReceiptPaymentEx.Credit7, fiscalReceipt.ReceiptPaymentEx.Credit8);
                _logger.Debug($"Called EndFiscalReceiptEx with params (extended payments). Response: {errorCode}");
            }
            else if (hasStandardPayment)
            {
                errorCode = _comManager.EndFiscalReceiptCurr(
                    fiscalReceipt.ReceiptPayment.Cash, fiscalReceipt.ReceiptPayment.Credit1, fiscalReceipt.ReceiptPayment.Credit2,
                    fiscalReceipt.ReceiptPayment.Credit3, fiscalReceipt.ReceiptPayment.Credit4, 0, 0, 0);
                _logger.Debug($"Called EndFiscalReceiptCurr with params (standard payments). Response: {errorCode}");
            }
            else
            {
                errorCode = _comManager.EndFiscalCacheReceipt();
                _logger.Debug($"No payment specified. Called EndFiscalCacheReceipt. Response: {errorCode}");
            }

            return errorCode;
        }

        private int ProcessNonFiscalReceipt(NonFiscalReceipt nonFiscalReceipt, ReceiptJson jsonReceipt)
        {
            if (nonFiscalReceipt == null) return 999;

            int errorCode = _comManager.BeginNonFiscalReceipt();
            _logger.Debug($"Called BeginNonFiscalReceipt. Response: {errorCode}");
            if (errorCode != 0) return errorCode;

            if (jsonReceipt.TopCommentLines != null)
            {
                foreach (var line in jsonReceipt.TopCommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                {
                    errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                    _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            if (nonFiscalReceipt.Tare != null)
            {
                foreach (var item in nonFiscalReceipt.Tare.Where(i => i != null))
                {
                    errorCode = _comManager.PrintTareItem(item.TareDescription, item.TareQuantity, item.TarePrice);
                    _logger.Debug($"Called PrintTareItem with params ('{item.TareDescription}', {item.TareQuantity}, {item.TarePrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (nonFiscalReceipt.DepositReceive != null)
            {
                foreach (var item in nonFiscalReceipt.DepositReceive.Where(i => i != null))
                {
                    errorCode = _comManager.PrintDepositReceive(item.DepositReceiveDesc, item.DepositReceiveQ, item.DepositReceivePrice);
                    _logger.Debug($"Called PrintDepositReceive with params ('{item.DepositReceiveDesc}', {item.DepositReceiveQ}, {item.DepositReceivePrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (nonFiscalReceipt.PrintTareDeposit != null)
            {
                foreach (var item in nonFiscalReceipt.PrintTareDeposit.Where(i => i != null))
                {
                    errorCode = _comManager.PrintTareDeposit(item.Description, item.Quantity, item.UnitPrice);
                    _logger.Debug($"Called PrintTareDeposit with params ('{item.Description}', {item.Quantity}, {item.UnitPrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (nonFiscalReceipt.PrintTareDepositVoid != null)
            {
                foreach (var item in nonFiscalReceipt.PrintTareDepositVoid.Where(i => i != null))
                {
                    errorCode = _comManager.PrintTareDepositVoid(item.Description, item.Quantity, item.UnitPrice);
                    _logger.Debug($"Called PrintTareDepositVoid with params ('{item.Description}', {item.Quantity}, {item.UnitPrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (nonFiscalReceipt.DepositReceiveCredit != null)
            {
                foreach (var item in nonFiscalReceipt.DepositReceiveCredit.Where(i => i != null))
                {
                    errorCode = _comManager.PrintDepositReceiveCredit(item.depositReceiveCreditDesc, item.DepositReceiveCreditQ, item.DepositReceiveCreditPrice);
                    _logger.Debug($"Called PrintDepositReceiveCredit with params ('{item.depositReceiveCreditDesc}', {item.DepositReceiveCreditQ}, {item.DepositReceiveCreditPrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (nonFiscalReceipt.DepositRefund != null)
            {
                foreach (var item in nonFiscalReceipt.DepositRefund.Where(i => i != null))
                {
                    errorCode = _comManager.PrintDepositRefund(item.DepositRefundDescription, item.DepositRefundQuantity, item.DepositRefundPrice);
                    _logger.Debug($"Called PrintDepositRefund with params ('{item.DepositRefundDescription}', {item.DepositRefundQuantity}, {item.DepositRefundPrice}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                    if (item.CommentLines != null)
                    {
                        foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                        {
                            errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                            _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                            if (errorCode != 0) return errorCode;
                        }
                    }
                }
            }

            if (jsonReceipt.BottomCommentLines != null)
            {
                foreach (var line in jsonReceipt.BottomCommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                {
                    errorCode = _comManager.PrintNonFiscalLine(line.CommentLine, line.CommentLineAttrib);
                    _logger.Debug($"Called PrintNonFiscalLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            if (jsonReceipt.SetFooter != null)
            {
                errorCode = _comManager.SetFooter(jsonReceipt.SetFooter.line1, jsonReceipt.SetFooter.line2, jsonReceipt.SetFooter.line3, jsonReceipt.SetFooter.line4);
                _logger.Debug($"Called SetFooter with params ('{jsonReceipt.SetFooter.line1}', etc). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            errorCode = _comManager.EndNonFiscalReceipt();
            _logger.Debug($"Called EndNonFiscalReceipt. Response: {errorCode}");
            return errorCode;
        }

        private int ProcessReturnReceipt(ReturnReceipt returnReceipt, ReceiptJson jsonReceipt)
        {
            if (returnReceipt == null || returnReceipt.ReceiptItem == null)
            {
                _logger.Warn("ReceiptItem property is null in a return receipt.");
                return 999;
            }

            int errorCode = _comManager.BeginFiscalReceipt();
            _logger.Debug($"Called BeginFiscalReceipt. Response: {errorCode}");
            if (errorCode != 0) return errorCode;

            if (jsonReceipt.TopCommentLines != null)
            {
                foreach (var line in jsonReceipt.TopCommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                {
                    errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                    _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            foreach (var item in returnReceipt.ReceiptItem.Where(i => i != null))
            {
                errorCode = _comManager.PrintRecItemEx(item.ItemDescription, item.ItemQuantity, item.ItemPrice, item.VatID, item.ItemUnit, item.ItemGroup);
                _logger.Debug($"Called PrintRecItemEx with params ('{item.ItemDescription}', {item.ItemQuantity}, {item.ItemPrice}, {item.VatID}, '{item.ItemUnit}', '{item.ItemGroup}'). Response: {errorCode}");
                if (errorCode != 0) return errorCode;

                if (item.ItemDiscount != null && item.ItemDiscount.ItemDiscountType != 999)
                {
                    errorCode = _comManager.DiscountAdditionForItem(item.ItemDiscount.ItemDiscountType, item.ItemDiscount.ItemDiscountAmount);
                    _logger.Debug($"Called DiscountAdditionForItem with params ({item.ItemDiscount.ItemDiscountType}, {item.ItemDiscount.ItemDiscountAmount}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
                if (item.CommentLines != null)
                {
                    foreach (var line in item.CommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                    {
                        errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                        _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                        if (errorCode != 0) return errorCode;
                    }
                }
            }

            if (returnReceipt.ReceiptDiscount != null && returnReceipt.ReceiptDiscount.ReceiptDiscountType != 999)
            {
                errorCode = _comManager.DiscountAdditionForReceipt(returnReceipt.ReceiptDiscount.ReceiptDiscountType, returnReceipt.ReceiptDiscount.ReceiptDiscountAmount);
                _logger.Debug($"Called DiscountAdditionForReceipt with params ({returnReceipt.ReceiptDiscount.ReceiptDiscountType}, {returnReceipt.ReceiptDiscount.ReceiptDiscountAmount}). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            if (jsonReceipt.BottomCommentLines != null)
            {
                foreach (var line in jsonReceipt.BottomCommentLines.Where(l => l != null && !string.IsNullOrEmpty(l.CommentLine)))
                {
                    errorCode = _comManager.PrintCommentLine(line.CommentLine, line.CommentLineAttrib);
                    _logger.Debug($"Called PrintCommentLine with params ('{line.CommentLine}', {line.CommentLineAttrib}). Response: {errorCode}");
                    if (errorCode != 0) return errorCode;
                }
            }

            if (returnReceipt.RefundReceiptInfo != null)
            {
                errorCode = _comManager.RefundReceiptInfo(returnReceipt.RefundReceiptInfo.ECR, returnReceipt.RefundReceiptInfo.ReceiptNo, returnReceipt.RefundReceiptInfo.DocNo);
                _logger.Debug($"Called RefundReceiptInfo with params ('{returnReceipt.RefundReceiptInfo.ECR}', '{returnReceipt.RefundReceiptInfo.ReceiptNo}', '{returnReceipt.RefundReceiptInfo.DocNo}'). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            if (returnReceipt.ReturnReceiptInfo != null)
            {
                errorCode = _comManager.RefundReceiptInfo(returnReceipt.ReturnReceiptInfo.ECR, returnReceipt.ReturnReceiptInfo.ReceiptNo, returnReceipt.ReturnReceiptInfo.DocNo);
                _logger.Debug($"Called RefundReceiptInfo with params ('{returnReceipt.ReturnReceiptInfo.ECR}', '{returnReceipt.ReturnReceiptInfo.ReceiptNo}', '{returnReceipt.ReturnReceiptInfo.DocNo}'). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            if (jsonReceipt.SetFooter != null)
            {
                errorCode = _comManager.SetFooter(jsonReceipt.SetFooter.line1, jsonReceipt.SetFooter.line2, jsonReceipt.SetFooter.line3, jsonReceipt.SetFooter.line4);
                _logger.Debug($"Called SetFooter with params ('{jsonReceipt.SetFooter.line1}', etc). Response: {errorCode}");
                if (errorCode != 0) return errorCode;
            }

            bool hasExtendedReturnPayment = (returnReceipt.GoodsReturnPaymentEx?.Cash + returnReceipt.GoodsReturnPaymentEx?.Credit1 + returnReceipt.GoodsReturnPaymentEx?.Credit2 + returnReceipt.GoodsReturnPaymentEx?.Credit3 + returnReceipt.GoodsReturnPaymentEx?.Credit4 + returnReceipt.GoodsReturnPaymentEx?.Credit5 + returnReceipt.GoodsReturnPaymentEx?.Credit6 + returnReceipt.GoodsReturnPaymentEx?.Credit7 + returnReceipt.GoodsReturnPaymentEx?.Credit8) > 0;
            bool hasStandardReturnPayment = (returnReceipt.GoodsReturnPayment?.Cash + returnReceipt.GoodsReturnPayment?.Credit1 + returnReceipt.GoodsReturnPayment?.Credit2 + returnReceipt.GoodsReturnPayment?.Credit3 + returnReceipt.GoodsReturnPayment?.Credit4) > 0;

            if (hasExtendedReturnPayment)
            {
                errorCode = _comManager.GoodsReturnEx(
                    returnReceipt.GoodsReturnPaymentEx.Cash, returnReceipt.GoodsReturnPaymentEx.Credit1, returnReceipt.GoodsReturnPaymentEx.Credit2,
                    returnReceipt.GoodsReturnPaymentEx.Credit3, returnReceipt.GoodsReturnPaymentEx.Credit4, returnReceipt.GoodsReturnPaymentEx.Credit5,
                    returnReceipt.GoodsReturnPaymentEx.Credit6, returnReceipt.GoodsReturnPaymentEx.Credit7, returnReceipt.GoodsReturnPaymentEx.Credit8);
                _logger.Debug($"Called GoodsReturnEx with params (extended payments). Response: {errorCode}");
            }
            else if (hasStandardReturnPayment)
            {
                errorCode = _comManager.GoodsReturnCurr(
                    returnReceipt.GoodsReturnPayment.Cash, returnReceipt.GoodsReturnPayment.Credit1, returnReceipt.GoodsReturnPayment.Credit2,
                    returnReceipt.GoodsReturnPayment.Credit3, returnReceipt.GoodsReturnPayment.Credit4, 0, 0, 0);
                _logger.Debug($"Called GoodsReturnCurr with params (standard payments). Response: {errorCode}");
            }
            else
            {
                errorCode = _comManager.GoodsReturnCacheReceipt();
                _logger.Debug($"No payment specified. Called GoodsReturnCacheReceipt. Response: {errorCode}");
            }

            return errorCode;
        }

        private int ProcessReport(Report report)
        {
            if (report == null || string.IsNullOrEmpty(report.ReportType)) return 999;

            int errorCode;
            switch (report.ReportType.ToLower())
            {
                case "minix":
                    errorCode = _comManager.PrintMiniXReport();
                    _logger.Debug($"Called PrintMiniXReport. Response: {errorCode}");
                    return errorCode;
                case "printx":
                    errorCode = _comManager.PrintXReport();
                    _logger.Debug($"Called PrintXReport. Response: {errorCode}");
                    return errorCode;
                case "printz":
                    errorCode = _comManager.PrintZReport();
                    _logger.Debug($"Called PrintZReport. Response: {errorCode}");
                    return errorCode;
                case "sumperiodic":
                    errorCode = _comManager.PrintSumPeriodicReport(report.DateFrom, report.DateTo);
                    _logger.Debug($"Called PrintSumPeriodicReport with params ('{report.DateFrom}', '{report.DateTo}'). Response: {errorCode}");
                    return errorCode;
                case "periodic":
                    errorCode = _comManager.PrintPeriodicReport(report.DateFrom, report.DateTo);
                    _logger.Debug($"Called PrintPeriodicReport with params ('{report.DateFrom}', '{report.DateTo}'). Response: {errorCode}");
                    return errorCode;
                case "sumperiodicbynumber":
                    errorCode = _comManager.PrintSumPeriodicReportByNumber(report.NoFrom, report.NoTo);
                    _logger.Debug($"Called PrintSumPeriodicReportByNumber with params ({report.NoFrom}, {report.NoTo}). Response: {errorCode}");
                    return errorCode;
                case "periodicbynumber":
                    errorCode = _comManager.PrintPeriodicReportByNumber(report.NoFrom, report.NoTo);
                    _logger.Debug($"Called PrintPeriodicReportByNumber with params ({report.NoFrom}, {report.NoTo}). Response: {errorCode}");
                    return errorCode;
                default:
                    _logger.Warn($"Invalid reportType: {report.ReportType}");
                    return 999;
            }
        }

        private int ProcessSpecialFunction(SpecialFunction specialFunction)
        {
            if (specialFunction == null || string.IsNullOrEmpty(specialFunction.Function)) return 999;

            int errorCode;
            switch (specialFunction.Function.ToLower())
            {
                case "moneyin":
                case "moneyincurr":
                    errorCode = _comManager.MoneyInCurr(0, specialFunction.Amount);
                    _logger.Debug($"Called MoneyInCurr with params ({0}, {specialFunction.Amount}). Response: {errorCode}");
                    return errorCode;
                case "moneyout":
                case "moneyoutcurr":
                    errorCode = _comManager.MoneyOutCurr(0, specialFunction.Amount);
                    _logger.Debug($"Called MoneyOutCurr with params ({0}, {specialFunction.Amount}). Response: {errorCode}");
                    return errorCode;
                case "transferprereceipt":
                    errorCode = _comManager.TransferPreReceipt(specialFunction.RecNo, specialFunction.Amount);
                    _logger.Debug($"Called TransferPreReceipt with params ('{specialFunction.RecNo}', {specialFunction.Amount}). Response: {errorCode}");
                    return errorCode;
                case "opencashdrawer":
                    errorCode = _comManager.OpenCashDrawer();
                    _logger.Debug($"Called OpenCashDrawer. Response: {errorCode}");
                    return errorCode;
                default:
                    _logger.Warn($"Invalid special function: {specialFunction.Function}");
                    return 999;
            }
        }
    }
}