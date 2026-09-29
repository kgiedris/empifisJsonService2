using empifisJsonAPI2;
using empifisJsonAPI2.JsonObjects;
using Xunit;

namespace empifisJsonService2.Tests;

public class ReceiptProcessorTests
{
    private const string FiscalReceipt = """
        {
          "receiptType": "fiscal",
          "topCommentLines": [{ "commentLine": "Top", "commentLineAttrib": 64 }],
          "fiscalReceipt": {
            "receiptItem": [{
              "itemDescription": "Milk", "itemQuantity": 1, "itemPrice": 2.5, "vatID": 0, "itemUnit": "vnt",
              "itemDiscount": { "itemDiscountType": 1, "itemDiscountAmount": -10 }
            }],
            "receiptPaymentEx": { "cash": 5 }
          }
        }
        """;

    private static (int errorCode, string message) Process(FakeFiscalDevice device, string json) =>
        new ReceiptProcessor(device).ProcessReceipt(JsonInput.Deserialize<ReceiptJson>(json, "test")!);

    [Fact]
    public void FiscalReceipt_PrintsInOrderAndEndsWithEndFiscalReceiptEx()
    {
        var device = new FakeFiscalDevice();

        var (errorCode, message) = Process(device, FiscalReceipt);

        Assert.Equal(0, errorCode);
        Assert.Equal("Success", message);
        Assert.Equal(new[] { "GetFiscalInfo", "BeginFiscalReceipt", "PrintCommentLine", "PrintRecItemEx", "DiscountAdditionForItem", "EndFiscalReceiptEx" },
            device.CallNames);
        Assert.Contains("EndFiscalReceiptEx(5, 0, 0, 0, 0, 0, 0, 0, 0)", device.Calls);
    }

    [Fact]
    public void FiscalReceipt_WithoutPayment_EndsAsCacheReceipt()
    {
        // Accepted behaviour: a receipt without a payment block is ended as a cache receipt.
        var device = new FakeFiscalDevice();

        var (errorCode, _) = Process(device, FiscalReceipt.Replace("\"receiptPaymentEx\": { \"cash\": 5 }", "\"receiptDiscount\": null"));

        Assert.Equal(0, errorCode);
        Assert.Equal("EndFiscalCacheReceipt", device.CallNames[^1]);
    }

    [Fact]
    public void ErrorMidReceipt_ResetsTheDeviceAndReturnsTheDescribedError()
    {
        var device = new FakeFiscalDevice().Returns("PrintRecItemEx", 24);

        var (errorCode, message) = Process(device, FiscalReceipt);

        Assert.Equal(24, errorCode);
        Assert.StartsWith("ERR_ITEM_PRICE", message);
        Assert.Contains("ResetFiscal", device.CallNames);
        Assert.DoesNotContain("EndFiscalReceiptEx", device.CallNames);
    }

    [Fact]
    public void TimeoutOnEnd_WhenTheDeviceStillCompletedTheReceipt_ReportsSuccessAndDoesNotReset()
    {
        var device = new FakeFiscalDevice { CompletesDespiteErrorOn = "EndFiscalReceiptEx" }.Returns("EndFiscalReceiptEx", 555);

        var (errorCode, message) = Process(device, FiscalReceipt);

        Assert.Equal(0, errorCode);
        Assert.Equal("Success", message);
        Assert.DoesNotContain("ResetFiscal", device.CallNames);
    }

    [Fact]
    public void TimeoutOnEnd_WhenTheReceiptWasNotCompleted_ReturnsTheTimeoutAndResets()
    {
        var device = new FakeFiscalDevice().Returns("EndFiscalReceiptEx", 555);

        var (errorCode, message) = Process(device, FiscalReceipt);

        Assert.Equal(555, errorCode);
        Assert.Contains("may still have been printed", message);
        Assert.Contains("ResetFiscal", device.CallNames);
    }

    [Fact]
    public void ReturnReceipt_WithBothInfoObjects_SendsRefundInfoOnceAndAcceptsDocumentNo()
    {
        var device = new FakeFiscalDevice();
        const string json = """
            {
              "receiptType": "return",
              "returnReceipt": {
                "receiptItem": [{ "itemDescription": "Milk", "itemQuantity": 1, "itemPrice": 9, "vatID": 0 }],
                "RefundReceiptInfo": { "ECR": "11", "ReceiptNo": "22", "DocumentNo": "33" },
                "ReturnReceiptInfo": { "ECR": "44", "ReceiptNo": "55", "DocNo": "66" },
                "GoodsReturnPaymentEx": { "cash": 9 }
              }
            }
            """;

        var (errorCode, _) = Process(device, json);

        Assert.Equal(0, errorCode);
        Assert.Equal(new[] { "RefundReceiptInfo(11, 22, 33)" }, device.Calls.Where(c => c.StartsWith("RefundReceiptInfo")));
        Assert.Equal("GoodsReturnEx", device.CallNames[^1]);
    }

    [Fact]
    public void NonFiscalReceipt_ReadsTheDepositFieldNamesFromTheManual()
    {
        var device = new FakeFiscalDevice();
        const string json = """
            {
              "receiptType": "nonFiscal",
              "nonFiscalReceipt": {
                "depositReceive": [{ "depositReceiveDesc": "Bottle", "depositReceiveQ": 2, "depositReceivePrice": 0.1 }],
                "depositReceiveCredit": [{ "depositReceiveCreditDesc": "Crate", "depositReceiveCreditQ": 1, "depositReceiveCreditPrice": 1.5 }]
              }
            }
            """;

        var (errorCode, _) = Process(device, json);

        Assert.Equal(0, errorCode);
        Assert.Contains("PrintDepositReceive(Bottle, 2, 0.1)", device.Calls);
        Assert.Contains("PrintDepositReceiveCredit(Crate, 1, 1.5)", device.Calls);
        Assert.Equal("EndNonFiscalReceipt", device.CallNames[^1]);
    }

    [Fact]
    public void NonFiscalReceipt_DoesNotReadTheReceiptCounter()
    {
        // The duplicate-receipt check only applies to fiscal and return receipts.
        var device = new FakeFiscalDevice();

        Process(device, """{ "receiptType": "nonFiscal", "nonFiscalReceipt": {} }""");

        Assert.DoesNotContain("GetFiscalInfo", device.CallNames);
    }

    [Fact]
    public void FullFiscalReceipt_CallsEverySectionInOrder()
    {
        var device = new FakeFiscalDevice();
        const string json = """
            {
              "receiptType": "fiscal",
              "topCommentLines": [{ "commentLine": "Top", "commentLineAttrib": 64 }, { "commentLine": "" }],
              "fiscalReceipt": {
                "receiptItem": [{ "itemDescription": "Milk", "itemQuantity": 1, "itemPrice": 2.5, "vatID": 0,
                                  "commentLines": [{ "commentLine": "Item note", "commentLineAttrib": 72 }] }],
                "DepositReceive": [{ "depositReceiveDesc": "Bottle", "depositReceiveQ": 1, "depositReceivePrice": 0.1 }],
                "PrintTareDeposit": [{ "Description": "Crate", "Quantity": 1, "UnitPrice": 3, "CommentLines": [{ "CommentLine": "Tare note" }] }],
                "PrintTareDepositVoid": [{ "Description": "Crate", "Quantity": 1, "UnitPrice": 3 }],
                "LinkPreReceipt": [{ "ReceiptNo": "145", "Amount": 1.5 }],
                "receiptDiscount": { "receiptDiscountType": 2, "receiptDiscountAmount": -1 },
                "receiptPayment": { "cash": 1, "credit1": 2 }
              },
              "SetFooter": { "line1": "F1", "line2": "F2", "line3": "F3", "line4": "F4" },
              "bottomCommentLines": [{ "commentLine": "Bottom", "commentLineAttrib": 64 }]
            }
            """;

        var (errorCode, _) = Process(device, json);

        Assert.Equal(0, errorCode);
        Assert.Equal(new[]
        {
            "GetFiscalInfo(2)", "BeginFiscalReceipt()", "PrintCommentLine(Top, 64)",
            "PrintRecItemEx(Milk, 1, 2.5, 0, vnt, GR)", "PrintCommentLine(Item note, 72)",
            "PrintDepositReceive(Bottle, 1, 0.1)",
            "PrintTareDeposit(Crate, 1, 3)", "PrintCommentLine(Tare note, 64)",
            "PrintTareDepositVoid(Crate, 1, 3)",
            "LinkPreReceipt(145, 1.5)",
            "DiscountAdditionForReceipt(2, -1)",
            "PrintCommentLine(Bottom, 64)",
            "SetFooter(F1, F2, F3, F4)",
            "EndFiscalReceiptEx(1, 2, 0, 0, 0, 0, 0, 0, 0)",
        }, device.Calls);
    }

    [Fact]
    public void FiscalReceipt_EndPreReceipt_EndsAsPreReceipt()
    {
        var device = new FakeFiscalDevice();

        Process(device, FiscalReceipt.Replace("\"receiptPaymentEx\": { \"cash\": 5 }", "\"EndPreReceipt\": { \"EndPreReceiptLine\": \"EndPreReceipt\" }"));

        Assert.Equal("EndPreReceipt", device.CallNames[^1]);
    }

    [Fact]
    public void ReturnReceipt_WithStandardPayment_EndsWithGoodsReturnCurr()
    {
        var device = new FakeFiscalDevice();
        const string json = """
            {
              "receiptType": "return",
              "returnReceipt": {
                "receiptItem": [{ "itemDescription": "Milk", "itemQuantity": 1, "itemPrice": 9, "vatID": 0 }],
                "receiptDiscount": { "receiptDiscountType": 1, "receiptDiscountAmount": -10 },
                "goodsReturnPayment": { "cash": 4, "credit1": 4.1 }
              }
            }
            """;

        Process(device, json);

        Assert.Contains("DiscountAdditionForReceipt(1, -10)", device.Calls);
        Assert.Equal("GoodsReturnCurr(4, 4.1, 0, 0, 0, 0, 0, 0)", device.Calls[^1]);
    }

    [Theory]
    [InlineData("""{ "receiptType": "report", "report": { "reportType": "sumPeriodic", "dateFrom": "20260901", "dateTo": "20260930" } }""", "PrintSumPeriodicReport(20260901, 20260930)")]
    [InlineData("""{ "receiptType": "report", "report": { "reportType": "PeriodicByNumber", "noFrom": 1, "noTo": 2 } }""", "PrintPeriodicReportByNumber(1, 2)")]
    [InlineData("""{ "receiptType": "special", "specialFunction": { "function": "moneyIn", "amount": "1" } }""", "MoneyInCurr(0, 1)")]
    [InlineData("""{ "receiptType": "special", "specialFunction": { "function": "transferPreReceipt", "RecNo": "7", "Amount": 1.5 } }""", "TransferPreReceipt(7, 1.5)")]
    public void ReportsAndSpecialFunctions_CallTheMatchingDeviceMethod(string json, string expectedCall)
    {
        var device = new FakeFiscalDevice();

        var (errorCode, _) = Process(device, json);

        Assert.Equal(0, errorCode);
        Assert.Equal(new[] { expectedCall }, device.Calls);
    }

    [Fact]
    public void UnknownFields_AreIgnoredAndTheRestIsRead()
    {
        var receipt = JsonInput.Deserialize<ReceiptJson>(
            """{ "receiptType": "report", "reportt": { "x": 1 }, "report": { "reportType": "printX" } }""", "test");

        Assert.Equal("printX", receipt!.Report.ReportType);
    }
}
