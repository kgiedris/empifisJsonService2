using empifisJsonAPI2.JsonObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration; // Add this using directive
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using System.IO;

namespace empifisJsonAPI2
{
    public static class FiscalApiHandler
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();

        public static void MapFiscalEndpoints(this WebApplication app)
        {
            var config = app.Configuration.Get<AppConfig>();
            bool isRadisonErrorMode = config.servicePort.radison_error?.ToLower() == "on";

            // Extract handler to register the same POST route for both /fiscalCommand and //fiscalCommand
            var fiscalHandler = async (HttpContext context, EmpifisComManager comManager) =>
            {
                var jsonResponse = new ResponseJson();
                var reader = new StreamReader(context.Request.Body);
                var jsonString = await reader.ReadToEndAsync();

                _logger.Info($"Received JSON request to /fiscalCommand:\n{jsonString}");

                var jsonCommand = JsonConvert.DeserializeObject<fiscalCommand>(jsonString);

                try
                {
                    if (jsonCommand?.Command == null)
                    {
                        jsonResponse.ErrorCode = 999;
                        jsonResponse.ErrorMessage = "Command property is missing or null.";
                    }
                    else
                    {
                        switch (jsonCommand.Command.ToLower())
                        {
                            case "resetfiscal":
                                jsonResponse.ErrorCode = comManager.ResetFiscal();
                                break;
                            case "getfiscalinfo":
                                if (jsonCommand.GetFiscalInfo != null)
                                {
                                    var getFiscalInfoResult = comManager.GetFiscalInfo(jsonCommand.GetFiscalInfo.InfoType);
                                    jsonResponse.ErrorCode = getFiscalInfoResult.errorCode;
                                    jsonResponse.ErrorMessage = getFiscalInfoResult.message;
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'GetFiscalInfo' object.";
                                }
                                break;
                            case "moneyincurr":
                                if (jsonCommand.MoneyInCurr != null)
                                {
                                    jsonResponse.ErrorCode = comManager.MoneyInCurr(0, jsonCommand.MoneyInCurr.Amount);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'MoneyInCurr' object.";
                                }
                                break;
                            case "moneyoutcurr":
                                if (jsonCommand.MoneyOutCurr != null)
                                {
                                    jsonResponse.ErrorCode = comManager.MoneyOutCurr(0, jsonCommand.MoneyOutCurr.Amount);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'MoneyOutCurr' object.";
                                }
                                break;
                            case "opencashdrawer":
                                jsonResponse.ErrorCode = comManager.OpenCashDrawer();
                                break;
                            case "skipprintreceipt":
                                jsonResponse.ErrorCode = comManager.SkipPrintReceipt();
                                break;
                            case "printzreport":
                                jsonResponse.ErrorCode = comManager.PrintZReport();
                                break;
                            case "printxreport":
                                jsonResponse.ErrorCode = comManager.PrintXReport();
                                break;
                            case "printminixreport":
                                jsonResponse.ErrorCode = comManager.PrintMiniXReport();
                                break;
                            case "printsumperiodicreport":
                                if (jsonCommand.PrintSumPeriodicReport != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintSumPeriodicReport(jsonCommand.PrintSumPeriodicReport.dateFrom, jsonCommand.PrintSumPeriodicReport.dateTo);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintSumPeriodicReport' object.";
                                }
                                break;
                            case "printperiodicreport":
                                if (jsonCommand.PrintPeriodicReport != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintPeriodicReport(jsonCommand.PrintPeriodicReport.dateFrom, jsonCommand.PrintPeriodicReport.dateTo);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintPeriodicReport' object.";
                                }
                                break;
                            case "printsumperiodicreportbynumber":
                                if (jsonCommand.PrintSumPeriodicReportByNumber != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintSumPeriodicReportByNumber(jsonCommand.PrintSumPeriodicReportByNumber.noFrom, jsonCommand.PrintSumPeriodicReportByNumber.noTo);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintSumPeriodicReportByNumber' object.";
                                }
                                break;
                            case "printperiodicreportbynumber":
                                if (jsonCommand.PrintPeriodicReportByNumber != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintPeriodicReportByNumber(jsonCommand.PrintPeriodicReportByNumber.noFrom, jsonCommand.PrintPeriodicReportByNumber.noTo);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintPeriodicReportByNumber' object.";
                                }
                                break;
                            case "customerdisplay2":
                                if (jsonCommand.CustomerDisplay2 != null)
                                {
                                    jsonResponse.ErrorCode = comManager.CustomerDisplay2(jsonCommand.CustomerDisplay2.Line1, jsonCommand.CustomerDisplay2.Line2);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'CustomerDisplay2' object.";
                                }
                                break;
                            case "customerdisplaypro":
                                if (jsonCommand.CustomerDisplayPro != null)
                                {
                                    jsonResponse.ErrorCode = comManager.CustomerDisplayPro(jsonCommand.CustomerDisplayPro.Line);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'CustomerDisplayPro' object.";
                                }
                                break;
                            case "beginnonfiscalreceipt":
                                jsonResponse.ErrorCode = comManager.BeginNonFiscalReceipt();
                                break;
                            case "printtareitem":
                                if (jsonCommand.PrintTareItem != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintTareItem(jsonCommand.PrintTareItem.Description, jsonCommand.PrintTareItem.Quantity, jsonCommand.PrintTareItem.Price);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintTareItem' object.";
                                }
                                break;
                            case "printtareitemvoid":
                                if (jsonCommand.PrintTareItemVoid != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintTareItemVoid(jsonCommand.PrintTareItemVoid.Description, jsonCommand.PrintTareItemVoid.Quantity, jsonCommand.PrintTareItemVoid.Price);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintTareItemVoid' object.";
                                }
                                break;
                            case "printtaredeposit":
                                if (jsonCommand.PrintTareDeposit != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintTareDeposit(jsonCommand.PrintTareDeposit.Description, jsonCommand.PrintTareDeposit.Quantity, jsonCommand.PrintTareDeposit.UnitPrice);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintTareDeposit' object.";
                                }
                                break;
                            case "printtaredepositvoid":
                                if (jsonCommand.PrintTareDepositVoid != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintTareDepositVoid(jsonCommand.PrintTareDepositVoid.Description, jsonCommand.PrintTareDepositVoid.Quantity, jsonCommand.PrintTareDepositVoid.UnitPrice);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintTareDepositVoid' object.";
                                }
                                break;
                            case "printdepositreceive":
                                if (jsonCommand.PrintDepositReceive != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintDepositReceive(jsonCommand.PrintDepositReceive.Description, jsonCommand.PrintDepositReceive.Quantity, jsonCommand.PrintDepositReceive.Price);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintDepositReceive' object.";
                                }
                                break;
                            case "printdepositreceivecredit":
                                if (jsonCommand.PrintDepositReceiveCredit != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintDepositReceiveCredit(jsonCommand.PrintDepositReceiveCredit.Description, jsonCommand.PrintDepositReceiveCredit.Quantity, jsonCommand.PrintDepositReceiveCredit.Price);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintDepositReceiveCredit' object.";
                                }
                                break;
                            case "printdepositrefund":
                                if (jsonCommand.PrintDepositRefund != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintDepositRefund(jsonCommand.PrintDepositRefund.Description, jsonCommand.PrintDepositRefund.Quantity, jsonCommand.PrintDepositRefund.Price);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintDepositRefund' object.";
                                }
                                break;
                            case "printbarcode":
                                if (jsonCommand.PrintBarCode != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintBarCode(jsonCommand.PrintBarCode.System, jsonCommand.PrintBarCode.Height, jsonCommand.PrintBarCode.BarCode);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintBarCode' object.";
                                }
                                break;
                            case "printnonfisc_inline":
                                if (jsonCommand.PrintNonFiscalLine != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintNonFiscalLine(jsonCommand.PrintNonFiscalLine.Line, jsonCommand.PrintNonFiscalLine.Atrrib);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintNonFiscalLine' object.";
                                }
                                break;
                            case "endnonfiscalreceipt":
                                jsonResponse.ErrorCode = comManager.EndNonFiscalReceipt();
                                break;
                            case "beginfiscalreceipt":
                                jsonResponse.ErrorCode = comManager.BeginFiscalReceipt();
                                break;
                            case "printrecitem":
                                if (jsonCommand.PrintRecItem != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintRecItem(jsonCommand.PrintRecItem.ItemDescription, jsonCommand.PrintRecItem.ItemQuantity, jsonCommand.PrintRecItem.ItemPrice, jsonCommand.PrintRecItem.VatID, jsonCommand.PrintRecItem.ItemUnit);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintRecItem' object.";
                                }
                                break;
                            case "printrecitemex":
                                if (jsonCommand.PrintRecItemEx != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintRecItemEx(jsonCommand.PrintRecItemEx.Description, jsonCommand.PrintRecItemEx.Quantity, jsonCommand.PrintRecItemEx.Price, jsonCommand.PrintRecItemEx.Vat, jsonCommand.PrintRecItemEx.Dimension, jsonCommand.PrintRecItemEx.Group);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintRecItemEx' object.";
                                }
                                break;
                            case "itemreturn":
                                if (jsonCommand.ItemReturn != null)
                                {
                                    jsonResponse.ErrorCode = comManager.ItemReturn(jsonCommand.ItemReturn.Description, jsonCommand.ItemReturn.Quantity, jsonCommand.ItemReturn.Price, jsonCommand.ItemReturn.Vat, jsonCommand.ItemReturn.Dimension, jsonCommand.ItemReturn.CurrPercent, jsonCommand.ItemReturn.CurrAbsolute);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'ItemReturn' object.";
                                }
                                break;
                            case "itemreturnex":
                                if (jsonCommand.ItemReturnEx != null)
                                {
                                    jsonResponse.ErrorCode = comManager.ItemReturnEx(jsonCommand.ItemReturnEx.Description, jsonCommand.ItemReturnEx.Quantity, jsonCommand.ItemReturnEx.Price, jsonCommand.ItemReturnEx.Vat, jsonCommand.ItemReturnEx.Dimension, jsonCommand.ItemReturnEx.Group, jsonCommand.ItemReturnEx.CurrPercent, jsonCommand.ItemReturnEx.CurrAbsolute);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'ItemReturnEx' object.";
                                }
                                break;
                            case "printdepositreceivevoid":
                                if (jsonCommand.PrintDepositReceiveVoid != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintDepositReceiveVoid(jsonCommand.PrintDepositReceiveVoid.Description, jsonCommand.PrintDepositReceiveVoid.Quantity, jsonCommand.PrintDepositReceiveVoid.Price);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintDepositReceiveVoid' object.";
                                }
                                break;
                            case "printcommentline":
                                if (jsonCommand.PrintCommentLine != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintCommentLine(jsonCommand.PrintCommentLine.CommentLine, jsonCommand.PrintCommentLine.CommentLineAttrib);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintCommentLine' object.";
                                }
                                break;
                            case "discountadditionforitem":
                                if (jsonCommand.DiscountAdditionForItem != null)
                                {
                                    jsonResponse.ErrorCode = comManager.DiscountAdditionForItem(jsonCommand.DiscountAdditionForItem.Type, jsonCommand.DiscountAdditionForItem.Amount);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'DiscountAdditionForItem' object.";
                                }
                                break;
                            case "discountadditionforreceipt":
                                if (jsonCommand.DiscountAdditionForReceipt != null)
                                {
                                    jsonResponse.ErrorCode = comManager.DiscountAdditionForReceipt(jsonCommand.DiscountAdditionForReceipt.Type, jsonCommand.DiscountAdditionForReceipt.Amount);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'DiscountAdditionForReceipt' object.";
                                }
                                break;
                            case "transferprereceipt":
                                if (jsonCommand.TransferPreReceipt != null)
                                {
                                    jsonResponse.ErrorCode = comManager.TransferPreReceipt(jsonCommand.TransferPreReceipt.ReceiptNo, jsonCommand.TransferPreReceipt.Amount);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'TransferPreReceipt' object.";
                                }
                                break;
                            case "endprereceipt":
                                jsonResponse.ErrorCode = comManager.EndPreReceipt();
                                break;
                            case "linkprereceipt":
                                if (jsonCommand.LinkPreReceipt != null)
                                {
                                    jsonResponse.ErrorCode = comManager.LinkPreReceipt(jsonCommand.LinkPreReceipt.ReceiptNo, jsonCommand.LinkPreReceipt.Amount);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'LinkPreReceipt' object.";
                                }
                                break;
                            case "endfiscalreceiptcurr":
                                if (jsonCommand.EndFiscalReceiptCurr != null)
                                {
                                    jsonResponse.ErrorCode = comManager.EndFiscalReceiptCurr(jsonCommand.EndFiscalReceiptCurr.rCash, jsonCommand.EndFiscalReceiptCurr.Credit1, jsonCommand.EndFiscalReceiptCurr.Credit2, jsonCommand.EndFiscalReceiptCurr.Credit3, jsonCommand.EndFiscalReceiptCurr.Credit4, jsonCommand.EndFiscalReceiptCurr.rCurrency1, jsonCommand.EndFiscalReceiptCurr.rCurrency2, jsonCommand.EndFiscalReceiptCurr.rCurrency3);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'EndFiscalReceiptCurr' object.";
                                }
                                break;
                            case "setcustomercontact":
                                if (jsonCommand.SetCustomerContact != null)
                                {
                                    jsonResponse.ErrorCode = comManager.SetCustomerContact(jsonCommand.SetCustomerContact.Contact);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'SetCustomerContact' object.";
                                }
                                break;
                            case "refundreceiptinfo":
                                if (jsonCommand.RefundReceiptInfo != null)
                                {
                                    jsonResponse.ErrorCode = comManager.RefundReceiptInfo(jsonCommand.RefundReceiptInfo.ECR, jsonCommand.RefundReceiptInfo.ReceiptNo, jsonCommand.RefundReceiptInfo.DocNo);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'RefundReceiptInfo' object.";
                                }
                                break;
                            case "goodsreturncurr":
                                if (jsonCommand.GoodsReturnCurr != null)
                                {
                                    jsonResponse.ErrorCode = comManager.GoodsReturnCurr(jsonCommand.GoodsReturnCurr.rCash, jsonCommand.GoodsReturnCurr.Credit1, jsonCommand.GoodsReturnCurr.Credit2, jsonCommand.GoodsReturnCurr.Credit3, jsonCommand.GoodsReturnCurr.Credit4, jsonCommand.GoodsReturnCurr.rCurrency1, jsonCommand.GoodsReturnCurr.rCurrency2, jsonCommand.GoodsReturnCurr.rCurrency3);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'GoodsReturnCurr' object.";
                                }
                                break;
                            case "endfiscalreceiptex":
                                if (jsonCommand.EndFiscalReceiptEx != null)
                                {
                                    jsonResponse.ErrorCode = comManager.EndFiscalReceiptEx(jsonCommand.EndFiscalReceiptEx.rCash, jsonCommand.EndFiscalReceiptEx.Credit1, jsonCommand.EndFiscalReceiptEx.Credit2, jsonCommand.EndFiscalReceiptEx.Credit3, jsonCommand.EndFiscalReceiptEx.Credit4, jsonCommand.EndFiscalReceiptEx.Credit5, jsonCommand.EndFiscalReceiptEx.Credit6, jsonCommand.EndFiscalReceiptEx.Credit7, jsonCommand.EndFiscalReceiptEx.Credit8);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'EndFiscalReceiptEx' object.";
                                }
                                break;
                            case "goodsreturnex":
                                if (jsonCommand.GoodsReturnEx != null)
                                {
                                    jsonResponse.ErrorCode = comManager.GoodsReturnEx(jsonCommand.GoodsReturnEx.rCash, jsonCommand.GoodsReturnEx.Credit1, jsonCommand.GoodsReturnEx.Credit2, jsonCommand.GoodsReturnEx.Credit3, jsonCommand.GoodsReturnEx.Credit4, jsonCommand.GoodsReturnEx.Credit5, jsonCommand.GoodsReturnEx.Credit6, jsonCommand.GoodsReturnEx.Credit7, jsonCommand.GoodsReturnEx.Credit8);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'GoodsReturnEx' object.";
                                }
                                break;
                            case "endrecpaymentex":
                                if (jsonCommand.EndRecPaymentEx != null)
                                {
                                    jsonResponse.ErrorCode = comManager.EndRecPaymentEx(jsonCommand.EndRecPaymentEx.rCash, jsonCommand.EndRecPaymentEx.Credit1, jsonCommand.EndRecPaymentEx.Credit2, jsonCommand.EndRecPaymentEx.Credit3, jsonCommand.EndRecPaymentEx.Credit4, jsonCommand.EndRecPaymentEx.Credit5, jsonCommand.EndRecPaymentEx.Credit6, jsonCommand.EndRecPaymentEx.Credit7, jsonCommand.EndRecPaymentEx.Credit8);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'EndRecPaymentEx' object.";
                                }
                                break;
                            case "endfiscalcachereceipt":
                                jsonResponse.ErrorCode = comManager.EndFiscalCacheReceipt();
                                break;
                            case "goodsreturncachereceipt":
                                jsonResponse.ErrorCode = comManager.GoodsReturnCacheReceipt();
                                break;
                            case "endrecpayment":
                                if (jsonCommand.EndRecPayment != null)
                                {
                                    jsonResponse.ErrorCode = comManager.EndRecPayment(jsonCommand.EndRecPayment.rCash, jsonCommand.EndRecPayment.Credit1, jsonCommand.EndRecPayment.Credit2, jsonCommand.EndRecPayment.Credit3, jsonCommand.EndRecPayment.Credit4, jsonCommand.EndRecPayment.rCurrency1, jsonCommand.EndRecPayment.rCurrency2, jsonCommand.EndRecPayment.rCurrency3);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'EndRecPayment' object.";
                                }
                                break;
                            case "printcopyoflastreceipt":
                                jsonResponse.ErrorCode = comManager.PrintCopyOfLastReceipt();
                                break;
                            case "printcopyofreceipt":
                                if (jsonCommand.PrintCopyOfReceipt != null)
                                {
                                    jsonResponse.ErrorCode = comManager.PrintCopyOfReceipt(jsonCommand.PrintCopyOfReceipt.From, jsonCommand.PrintCopyOfReceipt.To);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'PrintCopyOfReceipt' object.";
                                }
                                break;
                            case "setfooter":
                                if (jsonCommand.SetFooter != null)
                                {
                                    jsonResponse.ErrorCode = comManager.SetFooter(jsonCommand.SetFooter.line1, jsonCommand.SetFooter.line2, jsonCommand.SetFooter.line3, jsonCommand.SetFooter.line4);
                                }
                                else
                                {
                                    jsonResponse.ErrorCode = 999;
                                    jsonResponse.ErrorMessage = "Missing 'SetFooter' object.";
                                }
                                break;
                            default:
                                jsonResponse.ErrorCode = 999;
                                jsonResponse.ErrorMessage = "Command not implemented or invalid.";
                                break;
                        }

                        if (string.IsNullOrEmpty(jsonResponse.ErrorMessage))
                        {
                            jsonResponse.ErrorMessage = jsonResponse.ErrorCode == 0 ? "Success" : "Error";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Exception while processing fiscalCommand.");
                    jsonResponse.ErrorCode = 999;
                    jsonResponse.ErrorMessage = ex.Message;
                }

                object finalResponse;
                if (isRadisonErrorMode)
                {
                    _logger.Info("Radison error mode is ON. Creating ResponseJsonRadison.");
                    var radisonResponse = new ResponseJsonRadison
                    {
                        ErrorCode = jsonResponse.ErrorCode,
                        ErrorMessage = jsonResponse.ErrorMessage
                    };

                    var fiscalInfoCashRegister = comManager.GetFiscalInfo(3);
                    radisonResponse.CashRegisterNo = fiscalInfoCashRegister.message;

                    var fiscalInfoReceiptNo = comManager.GetFiscalInfo(2);
                    if (int.TryParse(fiscalInfoReceiptNo.message, out int recNo))
                    {
                        radisonResponse.ReceiptNo = (recNo - 1).ToString();
                    }
                    else
                    {
                        _logger.Warn($"Could not parse ReceiptNo from COM object: '{fiscalInfoReceiptNo.message}'");
                        radisonResponse.ReceiptNo = "N/A";
                    }
                    finalResponse = radisonResponse;
                }
                else
                {
                    finalResponse = jsonResponse;
                }

                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };
                string responseJsonString = JsonConvert.SerializeObject(finalResponse, jsonSerializerSettings);
                _logger.Info($"Sending JSON response from /fiscalCommand:\n{responseJsonString}");

                return Results.Ok(finalResponse);
            };

            // Register the handler for the canonical path. The middleware in Program.cs
            // will normalize duplicate slashes (e.g. //fiscalCommand -> /fiscalCommand).
            app.MapPost("/fiscalCommand", fiscalHandler);

            // Extract the /fullReceipt handler so the fallback dispatcher can call it too
            var fullReceiptHandler = async (HttpContext context, ReceiptProcessor receiptProcessor) =>
            {
                var jsonResponse = new ResponseJson();
                var reader = new StreamReader(context.Request.Body);
                var jsonString = await reader.ReadToEndAsync();

                _logger.Info($"Received JSON request to /fullReceipt:\n{jsonString}");

                var jsonReceipt = JsonConvert.DeserializeObject<ReceiptJson>(jsonString);

                try
                {
                    if (jsonReceipt?.ReceiptType == null)
                    {
                        jsonResponse.ErrorCode = 999;
                        jsonResponse.ErrorMessage = "ReceiptType is missing or null.";
                    }
                    else
                    {
                        var result = receiptProcessor.ProcessReceipt(jsonReceipt);
                        jsonResponse.ErrorCode = result.errorCode;
                        jsonResponse.ErrorMessage = result.message;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Exception while processing fullReceipt.");
                    jsonResponse.ErrorCode = 999;
                    jsonResponse.ErrorMessage = ex.Message;
                }

                object finalResponse;
                if (isRadisonErrorMode)
                {
                    _logger.Info("Radison error mode is ON. Creating ResponseJsonRadison.");
                    var radisonResponse = new ResponseJsonRadison
                    {
                        ErrorCode = jsonResponse.ErrorCode,
                        ErrorMessage = jsonResponse.ErrorMessage
                    };

                    var fiscalInfoCashRegister = app.Services.GetRequiredService<EmpifisComManager>().GetFiscalInfo(3);
                    radisonResponse.CashRegisterNo = fiscalInfoCashRegister.message;

                    var fiscalInfoReceiptNo = app.Services.GetRequiredService<EmpifisComManager>().GetFiscalInfo(2);
                    if (int.TryParse(fiscalInfoReceiptNo.message, out int recNo))
                    {
                        radisonResponse.ReceiptNo = (recNo - 1).ToString();
                    }
                    else
                    {
                        _logger.Warn($"Could not parse ReceiptNo from COM object: '{fiscalInfoReceiptNo.message}'");
                        radisonResponse.ReceiptNo = "N/A";
                    }
                    finalResponse = radisonResponse;
                }
                else
                {
                    finalResponse = jsonResponse;
                }

                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                };
                string responseJsonString = JsonConvert.SerializeObject(finalResponse, jsonSerializerSettings);
                _logger.Info($"Sending JSON response from /fullReceipt:\n{responseJsonString}");

                return Results.Ok(finalResponse);
            };

            // Register canonical route
            app.MapPost("/fullReceipt", fullReceiptHandler);

            // Fallback catch-all: if a request wasn't matched (for example because the
            // incoming raw target contained duplicate slashes) check the original raw
            // target saved by middleware and dispatch to the fiscal handler when it
            // contains a double-slash fiscalCommand. This allows requests like
            // http://localhost:5006//fiscalCommand to be handled even if routing
            // normalization prevented a direct match.
            app.MapPost("/{**catchall}", async (HttpContext context, EmpifisComManager comManager, ReceiptProcessor receiptProcessor) =>
            {
                try
                {
                    var originalRaw = context.Items.ContainsKey("originalRawTarget") ? context.Items["originalRawTarget"]?.ToString() : null;

                    if (string.IsNullOrEmpty(originalRaw))
                    {
                        return Results.NotFound();
                    }

                    // Strip query string and normalize consecutive slashes into one
                    var pathPart = originalRaw;
                    var qIdx = pathPart.IndexOf('?');
                    if (qIdx >= 0) pathPart = pathPart.Substring(0, qIdx);
                    var normalized = System.Text.RegularExpressions.Regex.Replace(pathPart, "/{2,}", "/");

                    // Dispatch based on normalized path
                    switch (normalized)
                    {
                        case "/fiscalCommand":
                            return await fiscalHandler(context, comManager);
                        case "/fullReceipt":
                            return await fullReceiptHandler(context, receiptProcessor);
                        default:
                            return Results.NotFound();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in catch-all fallback route.");
                    return Results.StatusCode(500);
                }
            });
        }
    }
}