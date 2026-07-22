using Ack05Enhanced.Core;
using Ack05Enhanced.Windows;

namespace Ack05Enhanced.Probe;

internal sealed class ConsoleConfiguredAck05InputSink : IAck05InputSink, IDisposable
{
    private Ack05ActionEngine _engine;
    private Ack05KeymapLayerRouter _layerRouter;
    private readonly SendInputAck05Output _output;
    private readonly Ack05ProfileSelector _profileSelector;
    private readonly IForegroundProcessProvider _foregroundProcessProvider;
    private readonly object _sync = new();
    private bool _disposed;

    internal ConsoleConfiguredAck05InputSink(
        Ack05Configuration configuration,
        IForegroundProcessProvider? foregroundProcessProvider = null,
        bool allowTargetLaunch = false)
    {
        _profileSelector = new Ack05ProfileSelector(configuration);
        _foregroundProcessProvider = foregroundProcessProvider ??
            new WindowsForegroundProcessProvider();
        var profile = _profileSelector.Select(processName: null);
        _engine = new Ack05ActionEngine(profile);
        _layerRouter = new Ack05KeymapLayerRouter(profile);
        _output = new SendInputAck05Output(
            new User32WindowsInputInjector(),
            allowTargetLaunch ? new ShellAck05TargetLauncher() : null);
        _output.TargetLaunchFailed += message => Console.Error.WriteLine($"TARGET: {message}");
    }

    internal async Task MonitorForegroundProfileAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            UpdateForegroundProfile();
        }
    }

    internal async Task MonitorTapHoldAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            lock (_sync)
            {
                try
                {
                    ApplyRouted(_layerRouter.Tick());
                }
                catch
                {
                    PanicRelease();
                    throw;
                }
            }
        }
    }

    public ValueTask OnConnectionStatusAsync(
        Ack05ConnectionStatus status,
        CancellationToken cancellationToken)
    {
        if (status.State is Ack05ConnectionState.Disconnected or Ack05ConnectionState.Stopped)
        {
            lock (_sync)
            {
                PanicRelease();
            }
        }

        var line = status.State switch
        {
            Ack05ConnectionState.WaitingForDevice when status.Message is null =>
                "Waiting for ACK05 vendor collection...",
            Ack05ConnectionState.WaitingForDevice =>
                $"Waiting for ACK05: {status.Message}",
            Ack05ConnectionState.Connected =>
                $"ACK05 connected. Active profile: {_engine.Profile.Id}",
            Ack05ConnectionState.Disconnected =>
                $"ACK05 disconnected: {status.Message}",
            Ack05ConnectionState.Stopped =>
                "ACK05 output service stopped; panic release complete.",
            _ => status.State.ToString(),
        };

        Console.WriteLine(line);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnInputAsync(
        LayerRoutedEvent input,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            try
            {
                ApplyRouted(_layerRouter.Route(
                    new PhysicalInputEvent(input.Control, input.InputType)));
            }
            catch
            {
                PanicRelease();
                throw;
            }
        }

        return ValueTask.CompletedTask;
    }

    private void ApplyRouted(IEnumerable<LayerRoutedEvent> routedEvents)
    {
        foreach (var routed in routedEvents)
        {
            if (routed.Kind == LayerRouteKind.LayerChanged)
            {
                Console.WriteLine($"layer -> {routed.Layer}");
                continue;
            }

            Console.WriteLine($"L{routed.Layer} {routed.Control} {routed.InputType}");
            foreach (var outputEvent in _engine.Apply(routed))
            {
                _output.Apply(outputEvent);
                Console.WriteLine($"  OUT {Format(outputEvent)}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                PanicRelease();
            }
        }
        finally
        {
            _output.Dispose();
            _disposed = true;
        }
    }

    private void PanicRelease()
    {
        var failures = new List<Exception>();
        try
        {
            foreach (var routed in _layerRouter.Reset())
            {
                if (routed.Kind == LayerRouteKind.Input)
                {
                    _output.Apply(_engine.Apply(routed));
                }
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            _output.Apply(_engine.Reset());
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            _output.ReleaseAll();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("ACK05 panic release failed.", failures);
        }
    }

    private void UpdateForegroundProfile()
    {
        var profile = _profileSelector.Select(
            _foregroundProcessProvider.GetForegroundProcessName());

        lock (_sync)
        {
            if (string.Equals(profile.Id, _engine.Profile.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                PanicRelease();
                _engine = new Ack05ActionEngine(profile);
                _layerRouter = new Ack05KeymapLayerRouter(profile);
                Console.WriteLine($"profile -> {profile.Id} ({profile.Name})");
            }
            catch
            {
                PanicRelease();
                throw;
            }
        }
    }

    private static string Format(Ack05OutputEvent output) => output.Kind switch
    {
        Ack05OutputEventKind.KeyDown => $"{output.Key} Down",
        Ack05OutputEventKind.KeyUp => $"{output.Key} Up",
        Ack05OutputEventKind.MouseWheel => $"Wheel {output.WheelSteps:+#;-#;0}",
        Ack05OutputEventKind.OpenTarget => $"Open {output.Target}",
        _ => output.Kind.ToString(),
    };
}
