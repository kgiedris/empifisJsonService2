using empifisJsonAPI2;
using Microsoft.Extensions.Options;
using Xunit;

namespace empifisJsonService2.Tests;

public sealed class WorkerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "empifis-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _in;
    private readonly string _out;
    private readonly FakeFiscalDevice _device = new();

    public WorkerTests()
    {
        _in = Path.Combine(_root, "in") + Path.DirectorySeparatorChar;
        _out = Path.Combine(_root, "out") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_in);
        Directory.CreateDirectory(_out);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private Worker CreateWorker() => new(_device, Options.Create(new AppConfig
    {
        servicePort = new ServicePortConfig { port = "5006", file_mode = "on", radison_error = "off", com_timeout_seconds = 45 },
        JsonPathConfig = new JsonPathConfig { InFilePath = _in, OutFilePath = _out },
    }), new ReceiptProcessor(_device));

    private async Task RunUntil(Func<bool> done)
    {
        var worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);
        try
        {
            for (int i = 0; i < 100 && !done(); i++) await Task.Delay(100);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
        Assert.True(done(), "The worker did not finish in time.");
    }

    // At start the worker reads version, cash register and state (GetFiscalInfo 4, 3, 9).
    private static readonly string[] StartupCheck = { "GetFiscalInfo(4)", "GetFiscalInfo(3)", "GetFiscalInfo(9)" };

    private List<string> DeviceCallsAfterStartupCheck() => _device.Calls.Skip(StartupCheck.Length).ToList();

    [Fact]
    public async Task Startup_ChecksThatTheFiscalDeviceAnswers()
    {
        await RunUntil(() => _device.Calls.Count >= StartupCheck.Length);

        Assert.Equal(StartupCheck, _device.Calls.Take(StartupCheck.Length));
    }

    [Fact]
    public async Task InterruptedRequest_IsQuarantinedAndAnswered557_NotResent()
    {
        File.WriteAllText(_in + "inReceipt_1.json.processing", """{ "receiptType": "report", "report": { "reportType": "printX" } }""");

        await RunUntil(() => File.Exists(_out + "outReceipt_1.json"));

        Assert.Empty(DeviceCallsAfterStartupCheck());
        Assert.Contains("\"ErrorCode\": 557", File.ReadAllText(_out + "outReceipt_1.json"));
        Assert.Single(Directory.GetFiles(Path.Combine(_in, "unconfirmed")));
        Assert.Empty(Directory.GetFiles(_in));
    }

    [Fact]
    public async Task InterruptedRequest_WithAResponseAlreadyWritten_KeepsThatResponse()
    {
        File.WriteAllText(_in + "inReceipt_2.json.processing", """{ "receiptType": "report", "report": { "reportType": "printX" } }""");
        File.WriteAllText(_out + "outReceipt_2.json", """{ "ErrorCode": 0, "ErrorMessage": "Success" }""");

        await RunUntil(() => Directory.Exists(Path.Combine(_in, "unconfirmed")) && Directory.GetFiles(Path.Combine(_in, "unconfirmed")).Length == 1);

        Assert.Contains("\"ErrorCode\": 0", File.ReadAllText(_out + "outReceipt_2.json"));
    }

    [Fact]
    public async Task Request_IsProcessed_WithoutDeletingOtherRecentResponses()
    {
        File.WriteAllText(_out + "outReceipt_recent.json", "{}");
        File.WriteAllText(_out + "outReceipt_old.json", "{}");
        File.SetLastWriteTime(_out + "outReceipt_old.json", DateTime.Now.AddDays(-2));
        File.WriteAllText(_in + "inReceipt_3.json", """{ "receiptType": "report", "report": { "reportType": "printX" } }""");

        await RunUntil(() => File.Exists(_out + "outReceipt_3.json") && !File.Exists(_in + "inReceipt_3.json.processing"));

        Assert.Equal(new[] { "PrintXReport()" }, DeviceCallsAfterStartupCheck());
        Assert.Contains("\"ErrorCode\": 0", File.ReadAllText(_out + "outReceipt_3.json"));
        Assert.True(File.Exists(_out + "outReceipt_recent.json"), "A recent response of another receipt was deleted.");
        Assert.False(File.Exists(_out + "outReceipt_old.json"), "A response older than a day was kept.");
        Assert.Empty(Directory.GetFiles(_in));
        Assert.Empty(Directory.GetFiles(_out, "*.tmp"));
    }
}
