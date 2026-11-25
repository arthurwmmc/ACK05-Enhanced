using Ack05Enhanced.Windows;
using static Ack05Enhanced.Windows.BleReadOnlyDiagnostic;

internal static class BleDiagnosticTests
{
    public static void Discovery()
    {
        var old = new BleCandidate("old", "ShortcutRemote", false);
        var current = new BleCandidate("new", "Shortcut Remote", true);
        var other = new BleCandidate("other", "Unrelated", true);
        Require(SelectCandidates([old, old, other]).SequenceEqual([old]), "duplicate endpoint");
        Require(SelectCandidates([old, current, other]).SequenceEqual([current]), "connected endpoint");
        Require(SelectCandidates([old with { Connected = true }, current]).Length == 2, "ambiguity lost");
        Require(SelectCandidates([other]).Length == 0, "unrelated endpoint");
    }

    public static void Inventory()
    {
        using var link = new FakeLink();
        var log = new List<string>();
        Require(Inspect(link, log), "expected FFE0 inventory");
        Require(link.Queried.SequenceEqual([DeviceInfo, Battery, Control]), "unexpected service access");
        Require(link.Reads.SequenceEqual([PnpId, Level]), "unexpected value read");
        Require(link.Observed.SequenceEqual([Ack, Reports]), "unexpected channel or order");
        Require(log.Any(x => x.Contains("73%")) && log.Any(x => x.Contains("Telink")), "metadata missing");
        Require(link.Listeners.Count == 0, "CCCD off should yield no listeners");
    }

    public static void FailClosed()
    {
        using var mismatch = new FakeLink { VendorMatches = false };
        Require(!Inspect(mismatch, []) && mismatch.Observed.Count == 0, "vendor mismatch");
        using var missing = new FakeLink { IncludeReports = false };
        Require(!Inspect(missing, []) && missing.Observed.Count == 0, "partial fingerprint");
        using var denied = new FakeLink { Denied = true };
        var log = new List<string>();
        Require(!Inspect(denied, log) && log.Contains("SharingViolation") && denied.Observed.Count == 0,
            "unavailable service");
        using var unknown = new FakeLink { Services = [Control] };
        log.Clear();
        Require(Inspect(unknown, log) && log.Any(x => x.Contains("Revision unknown")), "revision fallback");
    }

    public static void Frames()
    {
        Require(Describe(Reports, [2, 0xf2, 1, 42, 1]).Contains("42%"), "battery byte offset");
        Require(Describe(Ack, [2, 0xf2, 1, 42, 1]).StartsWith("0002"), "channel attribution");
        Require(Describe(Reports, [2, 0xf8, 0, 0]).StartsWith("F8"), "wake marker");
        Require(Describe(Reports, [2, 0xf0, 1, 0, 0, 0, 0, 1]).StartsWith("F0"), "activity");
        Require(Describe(Reports, [2, 0xf2, 1]).StartsWith("unclassified"), "truncated battery");
        Require(Describe(Reports, [2, 0xf2, 1, 255, 0]).StartsWith("unclassified"), "invalid percentage");
    }

    public static void Reconnect()
    {
        using var stop = new CancellationTokenSource();
        using var first = new FakeLink { Enabled = true };
        using var second = new FakeLink { Enabled = true };
        var received = 0;
        void Observe(FakeLink link) => InspectAsync(link, _ => { }, (_, _) => received++, stop.Token)
            .GetAwaiter().GetResult();
        Observe(first);
        first.Listeners[Reports]([2, 0xf0]);
        first.Lost.Cancel();
        Observe(second);
        first.Listeners[Reports]([2, 0xf0]);
        second.Listeners[Reports]([2, 0xf0]);
        stop.Cancel();
        second.Listeners[Reports]([2, 0xf0]);
        Require(received == 2, "late event after disconnect or stop");
    }

    private static bool Inspect(FakeLink link, List<string> log) =>
        InspectAsync(link, log.Add, (_, _) => throw new Exception("unsolicited callback"),
            CancellationToken.None).GetAwaiter().GetResult();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeLink : IReadOnlyBleLink
    {
        public bool IncludeReports = true, VendorMatches = true, Denied, Enabled;
        public Guid[] Services = [Control, TelinkOta, DeviceInfo, Battery, ShortId(0x1812)];
        public List<Guid> Queried = [], Reads = [], Observed = [];
        public Dictionary<Guid, Action<byte[]>> Listeners = [];
        public CancellationTokenSource Lost { get; } = new();
        public CancellationToken ConnectionLost => Lost.Token;
        public ulong Address => 0;
        public Task<Guid[]> ServicesAsync(CancellationToken token) => Task.FromResult(Services);
        public Task<BleField[]> FieldsAsync(Guid service, CancellationToken token)
        {
            Queried.Add(service);
            if (service == Control && Denied) throw new IOException("SharingViolation");
            if (service == Control)
            {
                var fields = new List<BleField> { new(Command, "Write", false, false), new(Ack, "Notify", false, true) };
                if (IncludeReports) fields.Add(new(Reports, "Notify", false, true));
                return Task.FromResult(fields.ToArray());
            }
            if (service != Battery && service != DeviceInfo) throw new Exception("unrelated service accessed");
            return Task.FromResult<BleField[]>([new(service == Battery ? Level : PnpId, "Read", true, false)]);
        }
        public Task<byte[]> ReadAsync(Guid service, Guid field, CancellationToken token)
        {
            Reads.Add(field);
            return Task.FromResult<byte[]>(field == Level ? [73] : [2, VendorMatches ? (byte)0xbd : (byte)0, 0x28, 2, 2, 0, 0]);
        }
        public Task<string> ObserveExistingAsync(Guid field, Action<byte[]> onValue, CancellationToken token)
        {
            Observed.Add(field);
            if (Enabled) Listeners.Add(field, onValue);
            return Task.FromResult(Enabled ? "already enabled" : "off");
        }
        public void Dispose() => Lost.Dispose();
    }
}
