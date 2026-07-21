namespace Ack05Enhanced.Core;

public enum LayerRouteKind
{
    Input,
    LayerChanged,
}

public readonly record struct LayerRoutedEvent(
    LayerRouteKind Kind,
    Ack05Control Control,
    PhysicalEventType InputType,
    int Layer,
    Ack05Action? ActionOverride = null);

public sealed class Ack05LayerRouter
{
    private readonly Dictionary<Ack05Control, int> _pressLayers = new();
    private readonly Func<DateTimeOffset> _now;
    private bool _modifierDown;
    private bool _modifierChordUsed;
    private bool _layerLatched;
    private DateTimeOffset _modifierPressedAt;

    public Ack05LayerRouter(
        Ack05LayerSwitchMode mode = Ack05LayerSwitchMode.Momentary,
        TimeSpan? tapHoldThreshold = null,
        Ack05Control? modifier = Ack05Control.K1,
        int baseLayer = 0,
        int alternateLayer = 1,
        Func<DateTimeOffset>? now = null)
    {
        if (baseLayer == alternateLayer)
        {
            throw new ArgumentException("Base and alternate layers must differ.");
        }

        var threshold = tapHoldThreshold ?? TimeSpan.FromMilliseconds(
            Ack05LayerSwitchSettings.DefaultTapHoldThresholdMs);
        if (threshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tapHoldThreshold),
                "Tap/hold threshold must be positive.");
        }

        Mode = mode;
        TapHoldThreshold = threshold;
        Modifier = modifier;
        BaseLayer = baseLayer;
        AlternateLayer = alternateLayer;
        ActiveLayer = baseLayer;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public Ack05LayerSwitchMode Mode { get; }

    public TimeSpan TapHoldThreshold { get; }

    public Ack05Control? Modifier { get; }

    public int BaseLayer { get; }

    public int AlternateLayer { get; }

    public int ActiveLayer { get; private set; }

    public IReadOnlyList<LayerRoutedEvent> Route(PhysicalInputEvent input)
    {
        if (Modifier is not null && input.Control == Modifier.Value)
        {
            return RouteModifier(input);
        }

        if (_modifierDown)
        {
            _modifierChordUsed = true;
        }

        if (input.Type == PhysicalEventType.Pulse)
        {
            return SingleInput(input, ActiveLayer);
        }

        if (input.Type == PhysicalEventType.Down)
        {
            _pressLayers[input.Control] = ActiveLayer;
            return SingleInput(input, ActiveLayer);
        }

        var layer = _pressLayers.Remove(input.Control, out var pressedLayer)
            ? pressedLayer
            : ActiveLayer;
        return SingleInput(input, layer);
    }

    public IReadOnlyList<LayerRoutedEvent> Reset()
    {
        var events = new List<LayerRoutedEvent>();

        foreach (var pair in _pressLayers.OrderBy(pair => pair.Key))
        {
            events.Add(new LayerRoutedEvent(
                LayerRouteKind.Input,
                pair.Key,
                PhysicalEventType.Up,
                pair.Value));
        }

        _pressLayers.Clear();
        _modifierDown = false;
        _modifierChordUsed = false;
        _layerLatched = false;

        if (ActiveLayer != BaseLayer)
        {
            ActiveLayer = BaseLayer;
            events.Add(LayerChange(BaseLayer));
        }

        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> RouteModifier(PhysicalInputEvent input)
    {
        if (input.Type == PhysicalEventType.Pulse)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        return input.Type == PhysicalEventType.Down
            ? ModifierDown()
            : ModifierUp();
    }

    private IReadOnlyList<LayerRoutedEvent> ModifierDown()
    {
        if (_modifierDown)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        _modifierDown = true;
        _modifierChordUsed = false;
        _modifierPressedAt = _now();

        if (Mode == Ack05LayerSwitchMode.Toggle)
        {
            _layerLatched = !_layerLatched;
            return ChangeLayer(_layerLatched ? AlternateLayer : BaseLayer);
        }

        return ChangeLayer(AlternateLayer);
    }

    private IReadOnlyList<LayerRoutedEvent> ModifierUp()
    {
        if (!_modifierDown)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        _modifierDown = false;
        if (Mode == Ack05LayerSwitchMode.Toggle)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        if (Mode == Ack05LayerSwitchMode.TapToggleHold)
        {
            var elapsed = _now() - _modifierPressedAt;
            if (!_modifierChordUsed && elapsed < TapHoldThreshold)
            {
                _layerLatched = !_layerLatched;
            }

            return ChangeLayer(_layerLatched ? AlternateLayer : BaseLayer);
        }

        return ChangeLayer(BaseLayer);
    }

    private IReadOnlyList<LayerRoutedEvent> ChangeLayer(int layer)
    {
        if (ActiveLayer == layer)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        ActiveLayer = layer;
        return new[] { LayerChange(layer) };
    }

    private static IReadOnlyList<LayerRoutedEvent> SingleInput(
        PhysicalInputEvent input,
        int layer)
    {
        return new[]
        {
            new LayerRoutedEvent(
                LayerRouteKind.Input,
                input.Control,
                input.Type,
                layer),
        };
    }

    private LayerRoutedEvent LayerChange(int layer)
    {
        return new LayerRoutedEvent(
            LayerRouteKind.LayerChanged,
            Modifier ?? Ack05Control.K1,
            PhysicalEventType.Pulse,
            layer);
    }
}
