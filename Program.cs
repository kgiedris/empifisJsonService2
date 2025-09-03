using empifisJsonAPI2;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using empifisJsonAPI2.JsonObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddNLog();
var logger = NLog.LogManager.GetCurrentClassLogger();

// Set default configuration values
var defaultSettings = new Dictionary<string, string>
{
    ["servicePort:port"] = "5006",
    ["servicePort:file_mode"] = "on",
    ["servicePort:radison_error"] = "off",
    ["servicePort:com_timeout_seconds"] = "45",
    ["JsonPathConfig:InFilePath"] = "C:\\Altera\\json\\in\\",
    ["JsonPathConfig:OutFilePath"] = "C:\\Altera\\json\\out\\"
};
builder.Configuration.AddInMemoryCollection(defaultSettings);

// Overwrite with values from config.json if the file exists
var configFilePath = @"C:\Altera\EmpifisJsonAPI\config.json";
try
{
    if (File.Exists(configFilePath))
    {
        builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
        logger.Info($"Configuration file found and loaded from '{configFilePath}'.");
    }
    else
    {
        logger.Warn($"Configuration file not found at '{configFilePath}'. Using default settings.");
    }
}
catch (Exception ex)
{
    logger.Fatal(ex, $"An error occurred while loading the configuration file from '{configFilePath}'. Application cannot start.");
    return;
}

// Bind configuration
builder.Services.Configure<AppConfig>(builder.Configuration);

// Add services for the API and worker
builder.Services.AddControllers();
builder.Services.AddSingleton<EmpifisComManager>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService();

// Get the port from configuration
var config = builder.Configuration.Get<AppConfig>();
var port = config.servicePort.port;

// Configure Kestrel to listen on the specified port and only use HTTP.
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

var app = builder.Build();

app.MapPost("/fiscalCommand", (EmpifisComManager comManager, [FromBody] fiscalCommand jsonCommand) =>
{
    var jsonResponse = new ResponseJson();

    var jsonSerializerSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented
    };

    try
    {
        logger.Debug($"Received JSON command: {JsonConvert.SerializeObject(jsonCommand, jsonSerializerSettings)}");

        // Handle potential null command or invalid format
        if (jsonCommand?.Command == null)
        {
            jsonResponse.ErrorCode = 999;
            jsonResponse.ErrorMessage = "Command property is missing or null.";
            return Results.Ok(jsonResponse);
        }

        // Use a case-insensitive switch for a more robust implementation
        switch (jsonCommand.Command.ToLower())
        {
            case "resetfiscal":
                jsonResponse.ErrorCode = comManager.ResetFiscal();
                break;
            case "getfiscalinfo":
                var getFiscalInfoResult = comManager.GetFiscalInfo(jsonCommand.GetFiscalInfo.InfoType);
                jsonResponse.ErrorCode = getFiscalInfoResult.errorCode;
                jsonResponse.ErrorMessage = getFiscalInfoResult.message;
                break;
            case "moneyincurr":
                jsonResponse.ErrorCode = comManager.MoneyInCurr(0, jsonCommand.MoneyInCurr.Amount);
                break;
            case "moneyoutcurr":
                jsonResponse.ErrorCode = comManager.MoneyOutCurr(0, jsonCommand.MoneyOutCurr.Amount);
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
                jsonResponse.ErrorCode = comManager.PrintSumPeriodicReport(jsonCommand.PrintSumPeriodicReport.dateFrom, jsonCommand.PrintSumPeriodicReport.dateTo);
                break;
            case "printperiodicreport":
                jsonResponse.ErrorCode = comManager.PrintPeriodicReport(jsonCommand.PrintPeriodicReport.dateFrom, jsonCommand.PrintPeriodicReport.dateTo);
                break;
            case "printsumperiodicreportbynumber":
                jsonResponse.ErrorCode = comManager.PrintSumPeriodicReportByNumber(jsonCommand.PrintSumPeriodicReportByNumber.noFrom, jsonCommand.PrintSumPeriodicReportByNumber.noTo);
                break;
            case "customerdisplay2":
                jsonResponse.ErrorCode = comManager.CustomerDisplay2(jsonCommand.CustomerDisplay2.Line1, jsonCommand.CustomerDisplay2.Line2);
                break;
            case "customerdisplaypro":
                jsonResponse.ErrorCode = comManager.CustomerDisplayPro(jsonCommand.CustomerDisplayPro.Line);
                break;
            case "beginnonfiscalreceipt":
                jsonResponse.ErrorCode = comManager.BeginNonFiscalReceipt();
                break;
            case "printtareitem":
                jsonResponse.ErrorCode = comManager.PrintTareItem(jsonCommand.PrintTareItem.Description, jsonCommand.PrintTareItem.Quantity, jsonCommand.PrintTareItem.Price);
                break;
            case "printtareitemvoid":
                jsonResponse.ErrorCode = comManager.PrintTareItemVoid(jsonCommand.PrintTareItemVoid.Description, jsonCommand.PrintTareItemVoid.Quantity, jsonCommand.PrintTareItemVoid.Price);
                break;
            case "printdepositreceive":
                jsonResponse.ErrorCode = comManager.PrintDepositReceive(jsonCommand.PrintDepositReceive.Description, jsonCommand.PrintDepositReceive.Quantity, jsonCommand.PrintDepositReceive.Price);
                break;
            case "printdepositreceivecredit":
                jsonResponse.ErrorCode = comManager.PrintDepositReceiveCredit(jsonCommand.PrintDepositReceiveCredit.Description, jsonCommand.PrintDepositReceiveCredit.Quantity, jsonCommand.PrintDepositReceiveCredit.Price);
                break;
            case "printdepositrefund":
                jsonResponse.ErrorCode = comManager.PrintDepositRefund(jsonCommand.PrintDepositRefund.Description, jsonCommand.PrintDepositRefund.Quantity, jsonCommand.PrintDepositRefund.Price);
                break;
            case "printbarcode":
                jsonResponse.ErrorCode = comManager.PrintBarCode(jsonCommand.PrintBarCode.System, jsonCommand.PrintBarCode.Height, jsonCommand.PrintBarCode.BarCode);
                break;
            case "printnonfisc_inline":
                jsonResponse.ErrorCode = comManager.PrintNonFiscalLine(jsonCommand.PrintNonFiscalLine.Line, jsonCommand.PrintNonFiscalLine.Atrrib);
                break;
            case "endnonfiscalreceipt":
                jsonResponse.ErrorCode = comManager.EndNonFiscalReceipt();
                break;
            case "beginfiscalreceipt":
                jsonResponse.ErrorCode = comManager.BeginFiscalReceipt();
                break;
            case "printrecitem":
                jsonResponse.ErrorCode = comManager.PrintRecItem(jsonCommand.PrintRecItem.ItemDescription, jsonCommand.PrintRecItem.ItemQuantity, jsonCommand.PrintRecItem.ItemPrice, jsonCommand.PrintRecItem.VatID, jsonCommand.PrintRecItem.ItemUnit);
                break;
            case "printrecitemex":
                jsonResponse.ErrorCode = comManager.PrintRecItemEx(jsonCommand.PrintRecItemEx.Description, jsonCommand.PrintRecItemEx.Quantity, jsonCommand.PrintRecItemEx.Price, jsonCommand.PrintRecItemEx.Vat, jsonCommand.PrintRecItemEx.Dimension, jsonCommand.PrintRecItemEx.Group);
                break;
            case "itemreturn":
                jsonResponse.ErrorCode = comManager.ItemReturn(jsonCommand.ItemReturn.Description, jsonCommand.ItemReturn.Quantity, jsonCommand.ItemReturn.Price, jsonCommand.ItemReturn.Vat, jsonCommand.ItemReturn.Dimension, jsonCommand.ItemReturn.CurrPercent, jsonCommand.ItemReturn.CurrAbsolute);
                break;
            case "itemreturnex":
                jsonResponse.ErrorCode = comManager.ItemReturnEx(jsonCommand.ItemReturnEx.Description, jsonCommand.ItemReturnEx.Quantity, jsonCommand.ItemReturnEx.Price, jsonCommand.ItemReturnEx.Vat, jsonCommand.ItemReturnEx.Dimension, jsonCommand.ItemReturnEx.Group, jsonCommand.ItemReturnEx.CurrPercent, jsonCommand.ItemReturnEx.CurrAbsolute);
                break;
            case "printdepositreceivevoid":
                jsonResponse.ErrorCode = comManager.PrintDepositReceiveVoid(jsonCommand.PrintDepositReceiveVoid.Description, jsonCommand.PrintDepositReceiveVoid.Quantity, jsonCommand.PrintDepositReceiveVoid.Price);
                break;
            case "printcommentline":
                jsonResponse.ErrorCode = comManager.PrintCommentLine(jsonCommand.PrintCommentLine.CommentLine, jsonCommand.PrintCommentLine.CommentLineAttrib);
                break;
            case "discountadditionforitem":
                jsonResponse.ErrorCode = comManager.DiscountAdditionForItem(jsonCommand.DiscountAdditionForItem.Type, jsonCommand.DiscountAdditionForItem.Amount);
                break;
            case "discountadditionforreceipt":
                jsonResponse.ErrorCode = comManager.DiscountAdditionForReceipt(jsonCommand.DiscountAdditionForReceipt.Type, jsonCommand.DiscountAdditionForReceipt.Amount);
                break;
            case "transferprereceipt":
                jsonResponse.ErrorCode = comManager.TransferPreReceipt(jsonCommand.TransferPreReceipt.ReceiptNo, jsonCommand.TransferPreReceipt.Amount);
                break;
            case "endprereceipt":
                jsonResponse.ErrorCode = comManager.EndPreReceipt();
                break;
            case "linkprereceipt":
                jsonResponse.ErrorCode = comManager.LinkPreReceipt(jsonCommand.LinkPreReceipt.ReceiptNo, jsonCommand.LinkPreReceipt.Amount);
                break;
            case "endfiscalreceiptcurr":
                jsonResponse.ErrorCode = comManager.EndFiscalReceiptCurr(jsonCommand.EndFiscalReceiptCurr.rCash, jsonCommand.EndFiscalReceiptCurr.Credit1, jsonCommand.EndFiscalReceiptCurr.Credit2, jsonCommand.EndFiscalReceiptCurr.Credit3, jsonCommand.EndFiscalReceiptCurr.Credit4, jsonCommand.EndFiscalReceiptCurr.rCurrency1, jsonCommand.EndFiscalReceiptCurr.rCurrency2, jsonCommand.EndFiscalReceiptCurr.rCurrency3);
                break;
            case "setcustomercontact":
                jsonResponse.ErrorCode = comManager.SetCustomerContact(jsonCommand.SetCustomerContact.Contact);
                break;
            case "refundreceiptinfo":
                jsonResponse.ErrorCode = comManager.RefundReceiptInfo(jsonCommand.RefundReceiptInfo.ECR, jsonCommand.RefundReceiptInfo.ReceiptNo, jsonCommand.RefundReceiptInfo.DocNo);
                break;
            case "goodsreturncurr":
                jsonResponse.ErrorCode = comManager.GoodsReturnCurr(jsonCommand.GoodsReturnCurr.rCash, jsonCommand.GoodsReturnCurr.Credit1, jsonCommand.GoodsReturnCurr.Credit2, jsonCommand.GoodsReturnCurr.Credit3, jsonCommand.GoodsReturnCurr.Credit4, jsonCommand.GoodsReturnCurr.rCurrency1, jsonCommand.GoodsReturnCurr.rCurrency2, jsonCommand.GoodsReturnCurr.rCurrency3);
                break;
            case "endfiscalreceiptex":
                jsonResponse.ErrorCode = comManager.EndFiscalReceiptEx(jsonCommand.EndFiscalReceiptEx.rCash, jsonCommand.EndFiscalReceiptEx.Credit1, jsonCommand.EndFiscalReceiptEx.Credit2, jsonCommand.EndFiscalReceiptEx.Credit3, jsonCommand.EndFiscalReceiptEx.Credit4, jsonCommand.EndFiscalReceiptEx.Credit5, jsonCommand.EndFiscalReceiptEx.Credit6, jsonCommand.EndFiscalReceiptEx.Credit7, jsonCommand.EndFiscalReceiptEx.Credit8);
                break;
            case "goodsreturnex":
                jsonResponse.ErrorCode = comManager.GoodsReturnEx(jsonCommand.GoodsReturnEx.rCash, jsonCommand.GoodsReturnEx.Credit1, jsonCommand.GoodsReturnEx.Credit2, jsonCommand.GoodsReturnEx.Credit3, jsonCommand.GoodsReturnEx.Credit4, jsonCommand.GoodsReturnEx.Credit5, jsonCommand.GoodsReturnEx.Credit6, jsonCommand.GoodsReturnEx.Credit7, jsonCommand.GoodsReturnEx.Credit8);
                break;
            case "endrecpaymentex":
                jsonResponse.ErrorCode = comManager.EndRecPaymentEx(jsonCommand.EndRecPaymentEx.rCash, jsonCommand.EndRecPaymentEx.Credit1, jsonCommand.EndRecPaymentEx.Credit2, jsonCommand.EndRecPaymentEx.Credit3, jsonCommand.EndRecPaymentEx.Credit4, jsonCommand.EndRecPaymentEx.Credit5, jsonCommand.EndRecPaymentEx.Credit6, jsonCommand.EndRecPaymentEx.Credit7, jsonCommand.EndRecPaymentEx.Credit8);
                break;
            case "endfiscalcachereceipt":
                jsonResponse.ErrorCode = comManager.EndFiscalCacheReceipt();
                break;
            case "goodsreturncachereceipt":
                jsonResponse.ErrorCode = comManager.GoodsReturnCacheReceipt();
                break;
            case "endrecpayment":
                jsonResponse.ErrorCode = comManager.EndRecPayment(jsonCommand.EndRecPayment.rCash, jsonCommand.EndRecPayment.Credit1, jsonCommand.EndRecPayment.Credit2, jsonCommand.EndRecPayment.Credit3, jsonCommand.EndRecPayment.Credit4, jsonCommand.EndRecPayment.rCurrency1, jsonCommand.EndRecPayment.rCurrency2, jsonCommand.EndRecPayment.rCurrency3);
                break;
            case "printcopyoflastreceipt":
                jsonResponse.ErrorCode = comManager.PrintCopyOfLastReceipt();
                break;
            case "printcopyofreceipt":
                jsonResponse.ErrorCode = comManager.PrintCopyOfReceipt(jsonCommand.PrintCopyOfReceipt.From, jsonCommand.PrintCopyOfReceipt.To);
                break;
            case "setfooter":
                jsonResponse.ErrorCode = comManager.SetFooter(jsonCommand.SetFooter.line1, jsonCommand.SetFooter.line2, jsonCommand.SetFooter.line3, jsonCommand.SetFooter.line4);
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
    catch (Exception ex)
    {
        logger.Error(ex, "Exception while processing fiscalCommand.");
        jsonResponse.ErrorCode = 999;
        jsonResponse.ErrorMessage = ex.Message;
    }

    logger.Debug($"Sending JSON response: {JsonConvert.SerializeObject(jsonResponse, jsonSerializerSettings)}");

    return Results.Ok(jsonResponse);
});

app.Run();