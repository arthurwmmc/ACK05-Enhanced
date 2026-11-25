using Ack05Enhanced.Windows;
using static Ack05Enhanced.Windows.BleReadOnlyDiagnostic;

internal static class NotificationTests
{
    public static void Cancellation() => Run(false).GetAwaiter().GetResult();
    public static void Disconnect() => Run(true).GetAwaiter().GetResult();

    private static async Task Run(bool disconnect)
    {
        using var stop = new CancellationTokenSource();
        using var lost = new CancellationTokenSource();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, lost.Token);
        var source = new Source();
        var lines = new List<string>();
        var capture = new BleNotificationCapture(lines.Add);
        var running = capture.ObserveAsync(source, () =>
        {
            source.Handlers[Ack]([0xab], "Connected");
            source.Handlers[Reports]([2, 0xf2, 0, 72, 0], "Connected");
        }, session.Token);
        var stale = source.Handlers[Ack];
        (disconnect ? lost : stop).Cancel();
        try { await running; } catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        stale([0xff], "Disconnected");
        if (!source.Operations.SequenceEqual(["read2", "read3", "listen2", "listen3", "detach3", "detach2"]) ||
            source.Handlers.Count != 0 || capture.AckCount != 1 || capture.ReportCount != 1 ||
            !lines.Any(l => l.Contains("size=1 hex=AB delta_ms=n/a")) ||
            !lines.Any(l => l.Contains("seq=2") && l.Contains("size=5 hex=02F2004800") && l.Contains("header=battery-candidate")))
            throw new Exception("Unexpected callback, packet log or GATT operation trace.");
    }

    public static void Disabled()
    {
        var source = new Source { SecondCccd = "None" };
        try
        {
            new BleNotificationCapture(_ => { }).ObserveAsync(source, () => throw new Exception("Unexpected ready"),
                CancellationToken.None).GetAwaiter().GetResult();
            throw new Exception("Disabled CCCD accepted");
        }
        catch (IOException) when (source.Operations.SequenceEqual(["read2", "read3"])) { }
    }

    private sealed class Source : IBleNotificationSource
    {
        internal string SecondCccd = "Notify";
        internal List<string> Operations = [];
        internal Dictionary<Guid, Action<byte[], string>> Handlers = [];
        public Task<string> ReadCccdAsync(Guid channel, CancellationToken token)
        {
            Operations.Add(channel == Ack ? "read2" : "read3");
            return Task.FromResult(channel == Ack ? "Notify" : SecondCccd);
        }
        public IDisposable Listen(Guid channel, Action<byte[], string> handler)
        {
            var suffix = channel == Ack ? "2" : "3";
            Operations.Add("listen" + suffix);
            Handlers.Add(channel, handler);
            return new Registration(() => { Operations.Add("detach" + suffix); Handlers.Remove(channel); });
        }
    }

    private sealed class Registration(Action detach) : IDisposable { public void Dispose() => detach(); }
}
