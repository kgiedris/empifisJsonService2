using System.Globalization;
using empifisJsonAPI2;

namespace empifisJsonService2.Tests;

/// <summary>
/// Records every device call and returns 0 unless a result is configured with <see cref="Returns"/>.
/// The next receipt number (GetFiscalInfo 2) goes up by one on every successful End* call, like the device.
/// </summary>
public sealed class FakeFiscalDevice : IFiscalDevice
{
    private readonly Dictionary<string, Queue<int>> _results = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public List<string> Calls { get; } = new();
    public long NextReceiptNo { get; set; } = 100;
    /// <summary>When set, the device finishes the named End* call (counter moves) even though it returns an error.</summary>
    public string? CompletesDespiteErrorOn { get; set; }

    /// <summary>The next calls to <paramref name="method"/> return these codes, in order.</summary>
    public FakeFiscalDevice Returns(string method, params int[] codes)
    {
        if (!_results.TryGetValue(method, out var queue)) _results[method] = queue = new Queue<int>();
        foreach (var code in codes) queue.Enqueue(code);
        return this;
    }

    public List<string> CallNames => Calls.Select(c => c.Split('(')[0]).ToList();

    private int Call(string method, params object?[] args)
    {
        Calls.Add($"{method}({string.Join(", ", args.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture)))})");
        int code = _results.TryGetValue(method, out var queue) && queue.Count > 0 ? queue.Dequeue() : 0;
        bool endsReceipt = method is nameof(EndFiscalReceiptEx) or nameof(EndFiscalCacheReceipt) or nameof(EndPreReceipt)
            or nameof(GoodsReturnEx) or nameof(GoodsReturnCurr) or nameof(GoodsReturnCacheReceipt);
        if (endsReceipt && (code == 0 || CompletesDespiteErrorOn == method)) NextReceiptNo++;
        return code;
    }

    public async Task<IDisposable> AcquireDeviceLockAsync()
    {
        await _lock.WaitAsync();
        return new Releaser(_lock);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }

    public (int errorCode, string message) GetFiscalInfo(int infoType)
    {
        Calls.Add($"GetFiscalInfo({infoType})");
        return infoType switch
        {
            2 => (0, NextReceiptNo.ToString()),
            3 => (0, "CR-TEST"),
            _ => (0, "info"),
        };
    }

    public int ResetFiscal() => Call(nameof(ResetFiscal));
    public int BeginFiscalReceipt() => Call(nameof(BeginFiscalReceipt));
    public int PrintCommentLine(string commentLine, int commentLineAttrib) => Call(nameof(PrintCommentLine), commentLine, commentLineAttrib);
    public int PrintRecItemEx(string description, double quantity, double price, int vat, string dimension, string group) => Call(nameof(PrintRecItemEx), description, quantity, price, vat, dimension, group);
    public int DiscountAdditionForItem(int type, double amount) => Call(nameof(DiscountAdditionForItem), type, amount);
    public int DiscountAdditionForReceipt(int type, double amount) => Call(nameof(DiscountAdditionForReceipt), type, amount);
    public int PrintDepositReceive(string description, double quantity, double price) => Call(nameof(PrintDepositReceive), description, quantity, price);
    public int PrintTareDeposit(string description, double quantity, double unitPrice) => Call(nameof(PrintTareDeposit), description, quantity, unitPrice);
    public int PrintTareDepositVoid(string description, double quantity, double unitPrice) => Call(nameof(PrintTareDepositVoid), description, quantity, unitPrice);
    public int LinkPreReceipt(string receiptNo, double amount) => Call(nameof(LinkPreReceipt), receiptNo, amount);
    public int SetFooter(string line1, string line2, string line3, string line4) => Call(nameof(SetFooter), line1, line2, line3, line4);
    public int EndPreReceipt() => Call(nameof(EndPreReceipt));
    public int EndFiscalReceiptEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => Call(nameof(EndFiscalReceiptEx), rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8);
    public int EndFiscalCacheReceipt() => Call(nameof(EndFiscalCacheReceipt));
    public int RefundReceiptInfo(string eCR, string receiptNo, string docNo) => Call(nameof(RefundReceiptInfo), eCR, receiptNo, docNo);
    public int GoodsReturnEx(double rCash, double credit1, double credit2, double credit3, double credit4, double credit5, double credit6, double credit7, double credit8) => Call(nameof(GoodsReturnEx), rCash, credit1, credit2, credit3, credit4, credit5, credit6, credit7, credit8);
    public int GoodsReturnCurr(double rCash, double credit1, double credit2, double credit3, double credit4, double rCurrency1, double rCurrency2, double rCurrency3) => Call(nameof(GoodsReturnCurr), rCash, credit1, credit2, credit3, credit4, rCurrency1, rCurrency2, rCurrency3);
    public int GoodsReturnCacheReceipt() => Call(nameof(GoodsReturnCacheReceipt));
    public int BeginNonFiscalReceipt() => Call(nameof(BeginNonFiscalReceipt));
    public int PrintNonFiscalLine(string line, int attrib) => Call(nameof(PrintNonFiscalLine), line, attrib);
    public int PrintTareItem(string description, double quantity, double price) => Call(nameof(PrintTareItem), description, quantity, price);
    public int PrintDepositReceiveCredit(string description, double quantity, double price) => Call(nameof(PrintDepositReceiveCredit), description, quantity, price);
    public int PrintDepositRefund(string description, double quantity, double price) => Call(nameof(PrintDepositRefund), description, quantity, price);
    public int EndNonFiscalReceipt() => Call(nameof(EndNonFiscalReceipt));
    public int PrintMiniXReport() => Call(nameof(PrintMiniXReport));
    public int PrintXReport() => Call(nameof(PrintXReport));
    public int PrintZReport() => Call(nameof(PrintZReport));
    public int PrintSumPeriodicReport(string dateFrom, string dateTo) => Call(nameof(PrintSumPeriodicReport), dateFrom, dateTo);
    public int PrintPeriodicReport(string dateFrom, string dateTo) => Call(nameof(PrintPeriodicReport), dateFrom, dateTo);
    public int PrintSumPeriodicReportByNumber(int noFrom, int noTo) => Call(nameof(PrintSumPeriodicReportByNumber), noFrom, noTo);
    public int PrintPeriodicReportByNumber(int noFrom, int noTo) => Call(nameof(PrintPeriodicReportByNumber), noFrom, noTo);
    public int MoneyInCurr(int paymentType, double amount) => Call(nameof(MoneyInCurr), paymentType, amount);
    public int MoneyOutCurr(int paymentType, double amount) => Call(nameof(MoneyOutCurr), paymentType, amount);
    public int TransferPreReceipt(string receiptNo, double amount) => Call(nameof(TransferPreReceipt), receiptNo, amount);
    public int OpenCashDrawer() => Call(nameof(OpenCashDrawer));
}
