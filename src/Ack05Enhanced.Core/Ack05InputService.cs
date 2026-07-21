namespace Ack05Enhanced.Core;

public sealed class Ack05InputService
{
    private readonly IAck05ReportSourceFactory _sourceFactory;
    private readonly IAck05InputSink _sink;
    private readonly Ack05InputServiceOptions _options;
    private Ack05ConnectionStatus? _lastStatus;

    public Ack05InputService(
        IAck05ReportSourceFactory sourceFactory,
        IAck05InputSink sink,
        Ack05InputServiceOptions? options = null)
    {
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _options = options ?? Ack05InputServiceOptions.Default;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await PublishStatusAsync(
                    new Ack05ConnectionStatus(Ack05ConnectionState.WaitingForDevice),
                    cancellationToken);

                IAck05ReportSource? source;
                try
                {
                    source = await _sourceFactory.TryOpenAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    await PublishStatusAsync(
                        new Ack05ConnectionStatus(
                            Ack05ConnectionState.WaitingForDevice,
                            exception.Message),
                        cancellationToken);
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                if (source is null)
                {
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                var disconnectMessage = await RunConnectionAsync(
                    source,
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await PublishStatusAsync(
                    new Ack05ConnectionStatus(
                        Ack05ConnectionState.Disconnected,
                        disconnectMessage),
                    cancellationToken);
                await DelayBeforeReconnectAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await PublishStatusAsync(
                new Ack05ConnectionStatus(Ack05ConnectionState.Stopped),
                CancellationToken.None);
        }
    }

    private async Task<string> RunConnectionAsync(
        IAck05ReportSource source,
        CancellationToken cancellationToken)
    {
        var layerSwitch = _options.LayerSwitch;
        var session = new Ack05InputSession(new Ack05LayerRouter(
            layerSwitch.Mode,
            TimeSpan.FromMilliseconds(layerSwitch.TapHoldThresholdMs),
            modifier: _options.UseKeymapLayers ? null : layerSwitch.Control));
        var disconnectMessage = "ACK05 input stream ended.";

        try
        {
            await PublishStatusAsync(
                new Ack05ConnectionStatus(Ack05ConnectionState.Connected),
                cancellationToken);

            var buffer = new byte[Math.Max(
                _options.MinimumReportBufferSize,
                source.InputReportLength)];

            while (!cancellationToken.IsCancellationRequested)
            {
                var count = await source.ReadAsync(buffer, cancellationToken);
                if (count == 0)
                {
                    throw new EndOfStreamException(
                        "ACK05 returned an empty read; it may have disconnected.");
                }

                foreach (var input in session.ProcessReport(buffer.AsSpan(0, count)))
                {
                    await _sink.OnInputAsync(input, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            disconnectMessage = exception.Message;
        }
        finally
        {
            try
            {
                foreach (var input in session.Reset())
                {
                    await _sink.OnInputAsync(input, CancellationToken.None);
                }
            }
            finally
            {
                try
                {
                    await source.DisposeAsync();
                }
                catch (Exception exception)
                {
                    disconnectMessage = exception.Message;
                }
            }
        }

        return disconnectMessage;
    }

    private async ValueTask PublishStatusAsync(
        Ack05ConnectionStatus status,
        CancellationToken cancellationToken)
    {
        if (_lastStatus == status)
        {
            return;
        }

        _lastStatus = status;
        await _sink.OnConnectionStatusAsync(status, cancellationToken);
    }

    private async Task DelayBeforeReconnectAsync(
        CancellationToken cancellationToken)
    {
        if (_options.ReconnectDelay > TimeSpan.Zero)
        {
            await Task.Delay(_options.ReconnectDelay, cancellationToken);
        }
    }
}
