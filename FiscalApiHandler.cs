using empifisJsonAPI2.JsonObjects;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.Extensions.Configuration; // Add this using directive

namespace empifisJsonAPI2
{
    public static class FiscalApiHandler
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();

        public static void MapFiscalEndpoints(this WebApplication app)
        {
            var config = app.Configuration.Get<AppConfig>();
            bool isRadisonErrorMode = config.servicePort.radison_error?.ToLower() == "on";

            app.MapPost("/fiscalCommand", async (HttpContext context, EmpifisComManager comManager) =>
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
                        // The switch logic from before
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
                            // ... (all other cases from previous code)
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
            });

            app.MapPost("/fullReceipt", async (HttpContext context, ReceiptProcessor receiptProcessor) =>
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
                        jsonResponse.ErrorCode = receiptProcessor.ProcessReceipt(jsonReceipt);
                        jsonResponse.ErrorMessage = jsonResponse.ErrorCode == 0 ? "Success" : "Error";
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
            });
        }
    }
}