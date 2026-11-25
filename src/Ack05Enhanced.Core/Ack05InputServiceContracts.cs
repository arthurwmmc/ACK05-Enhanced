namespace Ack05Enhanced.Core;

public interface IAck05ReportSource : IAsyncDisposable
{
    int InputReportLength { get; }

    ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken);
}

public interface IAck05ReportSourceFactory
{
    ValueTask<IAck05ReportSource?> TryOpenAsync(
        CancellationToken cancellationToken);
}

public enum Ack05ConnectionState
{
    WaitingForDevice,
    Connected,
    Disconnected,
    Stopped,
}

public readonly record struct Ack05ConnectionStatus(
    Ack05ConnectionState State,
    string? Message = null);

public interface IAck05InputSink
{
    ValueTask OnConnectionStatusAsync(
        Ack05ConnectionStatus status,
        CancellationToken cancellationToken);

    ValueTask OnInputAsync(
        LayerRoutedEvent input,
        CancellationToken cancellationToken);
}

/// <summary>
/// A sink that owns gesture state and releases its own logical outputs when capture ends.
/// </summary>
public interface IAck05GestureSink : IAck05InputSink
{
    void CancelCapture();
}

public sealed class Ack05InputServiceOptions
{
    public static Ack05InputServiceOptions Default { get; } = new(
        TimeSpan.FromSeconds(1),
        64);

    public Ack05InputServiceOptions(
        TimeSpan reconnectDelay,
        int minimumReportBufferSize = 64,
        Ack05LayerSwitchSettings? layerSwitch = null,
        bool useKeymapLayers = false)
    {
        if (reconnectDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reconnectDelay),
                "Reconnect delay cannot be negative.");
        }

        if (minimumReportBufferSize < Ack05RawReportDecoder.MinimumReportLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumReportBufferSize),
                $"Report buffer must be at least {Ack05RawReportDecoder.MinimumReportLength} bytes.");
        }

        ReconnectDelay = reconnectDelay;
        MinimumReportBufferSize = minimumReportBufferSize;
        LayerSwitch = layerSwitch ?? new Ack05LayerSwitchSettings();
        UseKeymapLayers = useKeymapLayers;
    }

    public TimeSpan ReconnectDelay { get; }

    public int MinimumReportBufferSize { get; }

    public Ack05LayerSwitchSettings LayerSwitch { get; }

    public bool UseKeymapLayers { get; }
}
