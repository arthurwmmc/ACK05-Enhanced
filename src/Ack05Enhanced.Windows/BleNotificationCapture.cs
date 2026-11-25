using System.Diagnostics;

namespace Ack05Enhanced.Windows;

public interface IBleNotificationSource
{
    Task<string> ReadCccdAsync(Guid channel, CancellationToken token);
    IDisposable Listen(Guid channel, Action<byte[], string> packet);
}

public sealed class BleNotificationCapture(Action<string> log)
{
    private readonly object _sync = new();
    private long? _previous;
    public int AckCount { get; private set; }
    public int ReportCount { get; private set; }

    public async Task ObserveAsync(IBleNotificationSource source, Action ready, CancellationToken token)
    {
        var channels = new[] { BleReadOnlyDiagnostic.Ack, BleReadOnlyDiagnostic.Reports };
        foreach (var channel in channels)
        {
            var state = await source.ReadCccdAsync(channel, token);
            log($"{channel} CCCD={state}");
            if (state != "Notify") throw new IOException("Both CCCDs must already be Notify.");
        }
        var active = true;
        var listeners = new Stack<IDisposable>();
        try
        {
            foreach (var channel in channels)
            {
                token.ThrowIfCancellationRequested();
                listeners.Push(source.Listen(channel, (bytes, connection) =>
                {
                    lock (_sync)
                    {
                        if (!active || token.IsCancellationRequested) return;
                        var now = Stopwatch.GetTimestamp();
                        var delta = _previous.HasValue ? Stopwatch.GetElapsedTime(_previous.Value, now).TotalMilliseconds.ToString("F3",
                            System.Globalization.CultureInfo.InvariantCulture) : "n/a";
                        _previous = now;
                        if (channel == BleReadOnlyDiagnostic.Ack) AckCount++; else ReportCount++;
                        var header = bytes.Length >= 2 && bytes[0] == 2 ? bytes[1] switch
                        {
                            0xf2 => "battery-candidate", 0xf8 => "protocol-event-candidate",
                            0xf0 => "control-report-candidate", _ => "unknown"
                        } : "unknown";
                        log($"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} seq={AckCount + ReportCount} channel={channel} size={bytes.Length} hex={Convert.ToHexString(bytes)} delta_ms={delta} connection={connection} header={header}");
                    }
                }));
                log($"{channel} ValueChanged attached");
            }
            ready();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        finally
        {
            lock (_sync) active = false;
            while (listeners.TryPop(out var listener)) listener.Dispose();
        }
    }
}
