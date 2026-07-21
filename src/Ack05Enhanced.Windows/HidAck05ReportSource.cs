using Ack05Enhanced.Core;

namespace Ack05Enhanced.Windows;

public sealed class HidAck05ReportSourceFactory : IAck05ReportSourceFactory
{
    private readonly bool _enableBitmask;

    public HidAck05ReportSourceFactory(bool enableBitmask)
    {
        _enableBitmask = enableBitmask;
    }

    public async ValueTask<IAck05ReportSource?> TryOpenAsync(
        CancellationToken cancellationToken)
    {
        var candidates = HidDiscovery.EnumerateAck05Collections()
            .Where(device => device.IsAck05VendorCollection)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple ACK05 vendor collections found; disconnect extra devices first.");
        }

        var device = candidates[0];
        var handle = HidDiscovery.OpenForMonitoring(device, _enableBitmask);
        FileStream stream;
        try
        {
            stream = new FileStream(
                handle,
                _enableBitmask ? FileAccess.ReadWrite : FileAccess.Read,
                Math.Max(64, (int)device.InputReportLength),
                isAsync: true);
        }
        catch
        {
            handle.Dispose();
            throw;
        }

        try
        {
            if (_enableBitmask)
            {
                await Ack05HidCommands.EnableBitmaskModeAsync(
                    stream,
                    device,
                    cancellationToken);
            }

            return new HidAck05ReportSource(stream, device.InputReportLength);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }
}

internal sealed class HidAck05ReportSource : IAck05ReportSource
{
    private readonly FileStream _stream;

    internal HidAck05ReportSource(FileStream stream, int inputReportLength)
    {
        _stream = stream;
        InputReportLength = inputReportLength;
    }

    public int InputReportLength { get; }

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken) =>
        _stream.ReadAsync(buffer, cancellationToken);

    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}
