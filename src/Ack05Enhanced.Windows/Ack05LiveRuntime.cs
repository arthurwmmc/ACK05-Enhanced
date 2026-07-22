using Ack05Enhanced.Core;

namespace Ack05Enhanced.Windows;

public enum Ack05LiveState
{
    Stopped,
    WaitingForDevice,
    Connected,
    Error,
}

public readonly record struct Ack05LiveStatus(
    Ack05LiveState State,
    string? Message = null);

public sealed class Ack05LiveRuntime : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly IAck05ReportSourceFactory _sourceFactory;
    private readonly IAck05WindowsInputInjector _inputInjector;
    private readonly IForegroundProcessProvider _foregroundProcessProvider;
    private readonly IAck05TargetLauncher _targetLauncher;
    private CancellationTokenSource? _cancellation;
    private ConfiguredAck05InputSink? _sink;
    private Task? _runTask;

    public event Action<Ack05LiveStatus>? StatusChanged;
    public event Action<string>? Notification;
    public event Action<Ack05RadialMenuState>? RadialMenuChanged;

    public Ack05LiveRuntime()
        : this(
            new HidAck05ReportSourceFactory(enableBitmask: true),
            new User32WindowsInputInjector(),
            new WindowsForegroundProcessProvider(),
            new ShellAck05TargetLauncher())
    {
    }

    public Ack05LiveRuntime(
        IAck05ReportSourceFactory sourceFactory,
        IAck05WindowsInputInjector inputInjector,
        IForegroundProcessProvider foregroundProcessProvider)
        : this(
            sourceFactory,
            inputInjector,
            foregroundProcessProvider,
            new ShellAck05TargetLauncher())
    {
    }

    public Ack05LiveRuntime(
        IAck05ReportSourceFactory sourceFactory,
        IAck05WindowsInputInjector inputInjector,
        IForegroundProcessProvider foregroundProcessProvider,
        IAck05TargetLauncher targetLauncher)
    {
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
        _foregroundProcessProvider = foregroundProcessProvider ??
            throw new ArgumentNullException(nameof(foregroundProcessProvider));
        _targetLauncher = targetLauncher ?? throw new ArgumentNullException(nameof(targetLauncher));
    }

    public bool IsActive => _runTask is not null;

    public bool IsRunning => _runTask is { IsCompleted: false };

    public async Task StartAsync(
        Ack05Configuration configuration,
        CancellationToken cancellationToken = default)
    {
        Validate(configuration);
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
            {
                return;
            }

            CleanupCompletedRuntime();

            var cancellation = new CancellationTokenSource();
            var sink = new ConfiguredAck05InputSink(
                configuration,
                _inputInjector,
                _foregroundProcessProvider,
                _targetLauncher);
            sink.ConnectionStatusChanged += OnConnectionStatusChanged;
            sink.TargetLaunchFailed += OnTargetLaunchFailed;
            sink.RadialMenuChanged += OnRadialMenuChanged;
            var service = new Ack05InputService(
                _sourceFactory,
                sink,
                new Ack05InputServiceOptions(
                    TimeSpan.FromSeconds(1),
                    64,
                    useKeymapLayers: true));

            _cancellation = cancellation;
            _sink = sink;
            _runTask = RunAsync(service, sink, cancellation);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public void UpdateConfiguration(Ack05Configuration configuration)
    {
        Validate(configuration);
        var sink = _sink;
        if (sink is null || !IsRunning)
        {
            return;
        }

        sink.UpdateConfiguration(configuration);
    }

    public void CancelRadialMenu() => _sink?.CancelRadialMenu();

    public async Task StopAsync()
    {
        Task? runTask;
        CancellationTokenSource? cancellation;

        await _lifecycle.WaitAsync();
        try
        {
            runTask = _runTask;
            cancellation = _cancellation;
            cancellation?.Cancel();
        }
        finally
        {
            _lifecycle.Release();
        }

        if (runTask is not null)
        {
            await runTask;
        }

        await _lifecycle.WaitAsync();
        try
        {
            if (ReferenceEquals(_runTask, runTask))
            {
                CleanupCompletedRuntime();
            }
        }
        finally
        {
            _lifecycle.Release();
        }

        Publish(new Ack05LiveStatus(Ack05LiveState.Stopped));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycle.Dispose();
    }

    private async Task RunAsync(
        Ack05InputService service,
        ConfiguredAck05InputSink sink,
        CancellationTokenSource cancellation)
    {
        var serviceTask = service.RunAsync(cancellation.Token);
        var profileTask = sink.MonitorForegroundProfileAsync(cancellation.Token);
        var tapHoldTask = sink.MonitorTapHoldAsync(cancellation.Token);
        try
        {
            var completed = await Task.WhenAny(serviceTask, profileTask, tapHoldTask);
            await completed;
            if (!cancellation.IsCancellationRequested)
            {
                Publish(new Ack05LiveStatus(
                    Ack05LiveState.Error,
                    "ACK05 live service stopped unexpectedly."));
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Publish(new Ack05LiveStatus(Ack05LiveState.Error, exception.Message));
        }
        finally
        {
            cancellation.Cancel();
            await AwaitCancellationAsync(serviceTask);
            await AwaitCancellationAsync(profileTask);
            await AwaitCancellationAsync(tapHoldTask);
        }
    }

    private void OnConnectionStatusChanged(Ack05ConnectionStatus status)
    {
        var liveStatus = status.State switch
        {
            Ack05ConnectionState.WaitingForDevice => new Ack05LiveStatus(
                Ack05LiveState.WaitingForDevice,
                status.Message),
            Ack05ConnectionState.Connected => new Ack05LiveStatus(Ack05LiveState.Connected),
            Ack05ConnectionState.Disconnected => new Ack05LiveStatus(
                Ack05LiveState.WaitingForDevice,
                status.Message),
            Ack05ConnectionState.Stopped => new Ack05LiveStatus(Ack05LiveState.Stopped),
            _ => new Ack05LiveStatus(Ack05LiveState.Error, status.Message),
        };
        Publish(liveStatus);
    }

    private void CleanupCompletedRuntime()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Cannot clean up a running ACK05 runtime.");
        }

        if (_sink is not null)
        {
            _sink.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _sink.TargetLaunchFailed -= OnTargetLaunchFailed;
            _sink.RadialMenuChanged -= OnRadialMenuChanged;
            _sink.Dispose();
        }

        _cancellation?.Dispose();
        _sink = null;
        _cancellation = null;
        _runTask = null;
    }

    private static async Task AwaitCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static void Validate(Ack05Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = Ack05ConfigurationValidator.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(errors[0]);
        }
    }

    private void Publish(Ack05LiveStatus status) => StatusChanged?.Invoke(status);

    private void OnTargetLaunchFailed(string message) => Notification?.Invoke(message);

    private void OnRadialMenuChanged(Ack05RadialMenuState state) =>
        RadialMenuChanged?.Invoke(state);
}

internal sealed class ConfiguredAck05InputSink : IAck05InputSink, IDisposable
{
    private readonly IForegroundProcessProvider _foregroundProcessProvider;
    private readonly SendInputAck05Output _output;
    private readonly object _sync = new();
    private Ack05ProfileSelector _profileSelector;
    private Ack05ActionEngine _engine;
    private Ack05KeymapLayerRouter _layerRouter;
    private bool _disposed;

    internal ConfiguredAck05InputSink(
        Ack05Configuration configuration,
        IAck05WindowsInputInjector inputInjector,
        IForegroundProcessProvider foregroundProcessProvider,
        IAck05TargetLauncher targetLauncher)
    {
        _profileSelector = new Ack05ProfileSelector(configuration);
        var profile = _profileSelector.Select(processName: null);
        _engine = new Ack05ActionEngine(profile);
        _layerRouter = new Ack05KeymapLayerRouter(profile);
        _output = new SendInputAck05Output(inputInjector, targetLauncher);
        _output.TargetLaunchFailed += message => TargetLaunchFailed?.Invoke(message);
        _foregroundProcessProvider = foregroundProcessProvider;
    }

    internal event Action<Ack05ConnectionStatus>? ConnectionStatusChanged;
    internal event Action<string>? TargetLaunchFailed;
    internal event Action<Ack05RadialMenuState>? RadialMenuChanged;

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

    internal void UpdateConfiguration(Ack05Configuration configuration)
    {
        var selector = new Ack05ProfileSelector(configuration);
        var profile = selector.Select(_foregroundProcessProvider.GetForegroundProcessName());

        lock (_sync)
        {
            PanicRelease();
            _profileSelector = selector;
            _engine = new Ack05ActionEngine(profile);
            _layerRouter = new Ack05KeymapLayerRouter(profile);
        }
    }

    internal void CancelRadialMenu()
    {
        lock (_sync)
        {
            ApplyRouted(_layerRouter.CancelRadialMenu());
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

        ConnectionStatusChanged?.Invoke(status);
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
            if (routed.Kind == LayerRouteKind.RadialMenuChanged &&
                routed.RadialMenu is { } radialMenu)
            {
                RadialMenuChanged?.Invoke(radialMenu);
            }
            else if (routed.Kind == LayerRouteKind.Input)
            {
                _output.Apply(_engine.Apply(routed));
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

    private void UpdateForegroundProfile()
    {
        var processName = _foregroundProcessProvider.GetForegroundProcessName();

        lock (_sync)
        {
            var profile = _profileSelector.Select(processName);
            if (string.Equals(profile.Id, _engine.Profile.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                PanicRelease();
                _engine = new Ack05ActionEngine(profile);
                _layerRouter = new Ack05KeymapLayerRouter(profile);
            }
            catch
            {
                PanicRelease();
                throw;
            }
        }
    }

    private void PanicRelease()
    {
        var failures = new List<Exception>();
        try
        {
            foreach (var routed in _layerRouter.Reset())
            {
                if (routed.Kind == LayerRouteKind.RadialMenuChanged &&
                    routed.RadialMenu is { } radialMenu)
                {
                    RadialMenuChanged?.Invoke(radialMenu);
                }
                else if (routed.Kind == LayerRouteKind.Input)
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
}
