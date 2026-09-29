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
    public void UnknownFields_AreIgnoredAndTheRestIsRead()
    {
        var receipt = JsonInput.Deserialize<ReceiptJson>(
            """{ "receiptType": "report", "reportt": { "x": 1 }, "report": { "reportType": "printX" } }""", "test");

        Assert.Equal("printX", receipt!.Report.ReportType);
    }
}
