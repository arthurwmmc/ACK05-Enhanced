namespace Ack05Enhanced.Core;

/// <summary>
/// Resolves a physical control against the active keymap layers. Higher active
/// layers win and Transparent falls through, matching the QMK/Vial layer model.
/// </summary>
public sealed class Ack05KeymapLayerRouter
{
    private readonly Dictionary<(int Layer, Ack05Control Control), Ack05Action> _bindings;
    private readonly Dictionary<Ack05Control, ActivePress> _presses = new();
    private readonly Dictionary<int, int> _momentaryCounts = new();
    private readonly HashSet<int> _toggledLayers = new();
    private readonly IReadOnlyDictionary<string, Ack05TapHoldDefinition> _tapHolds;
    private readonly Ack05ComboDefinition[] _combos;
    private readonly int _comboDefaultTermMs;
    private readonly List<ActiveCombo> _activeCombos = [];
    private readonly Func<DateTimeOffset> _now;
    private readonly int _layerCount;
    private PendingCombo? _pendingCombo;

    public Ack05KeymapLayerRouter(Ack05Profile profile, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _bindings = profile.Layers
            .SelectMany(layer => layer.Bindings.Select(binding =>
                new KeyValuePair<(int, Ack05Control), Ack05Action>(
                    (layer.Index, binding.Control),
                    binding.Action)))
            .ToDictionary();
        _layerCount = profile.Layers.Length;
        _tapHolds = profile.TapHolds.ToDictionary(
            definition => definition.Id,
            StringComparer.OrdinalIgnoreCase);
        _combos = profile.Combos;
        _comboDefaultTermMs = profile.ComboDefaults.TermMs;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public int ActiveLayer => ActiveLayers().Max();

    public IReadOnlyList<LayerRoutedEvent> Route(PhysicalInputEvent input)
    {
        var events = new List<LayerRoutedEvent>();
        events.AddRange(TickTapHolds());
        var occurredAt = _now();

        if (input.Type != PhysicalEventType.Pulse)
        {
            var active = _activeCombos.FirstOrDefault(combo =>
                combo.Definition.Controls.Contains(input.Control));
            if (active is not null)
            {
                events.AddRange(RouteActiveCombo(active, input));
                return events;
            }

            events.AddRange(RouteCombo(input, occurredAt));
            return events;
        }

        if (_pendingCombo is not null)
        {
            events.AddRange(FailPendingCombo());
        }

        events.AddRange(RouteResolved(input, occurredAt));
        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> RouteResolved(
        PhysicalInputEvent input,
        DateTimeOffset occurredAt)
    {
        var events = new List<LayerRoutedEvent>();

        if (input.Type == PhysicalEventType.Down)
        {
            events.AddRange(ResolveTapHoldInterrupt(input.Control, isRelease: false));
        }
        else if (input.Type == PhysicalEventType.Up)
        {
            events.AddRange(ResolveTapHoldInterrupt(input.Control, isRelease: true));
        }

        if (input.Type is PhysicalEventType.Down or PhysicalEventType.Pulse)
        {
            MarkTapHoldChordUsed(input.Control);
        }

        events.AddRange(input.Type switch
        {
            PhysicalEventType.Down => RouteDown(input.Control, occurredAt),
            PhysicalEventType.Up => RouteUp(input.Control),
            PhysicalEventType.Pulse => RoutePulse(input.Control),
            _ => Array.Empty<LayerRoutedEvent>(),
        });
        return events;
    }

    /// <summary>
    /// Resolves Tap & Hold definitions whose tapping term has expired. Hosts call
    /// this from a short periodic timer so TimeoutOnly works without new input.
    /// </summary>
    public IReadOnlyList<LayerRoutedEvent> Tick()
    {
        var events = new List<LayerRoutedEvent>();
        events.AddRange(TickCombo());
        events.AddRange(TickTapHolds());
        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> TickTapHolds()
    {
        var events = new List<LayerRoutedEvent>();
        var now = _now();
        foreach (var pair in _presses
            .Where(pair => pair.Value.TapHoldState == TapHoldState.Pending)
            .OrderBy(pair => pair.Value.PressedAt)
            .ToArray())
        {
            var definition = pair.Value.TapHoldDefinition!;
            if (now - pair.Value.PressedAt >= TimeSpan.FromMilliseconds(definition.TappingTermMs))
            {
                events.AddRange(ConfirmTapHold(pair.Key, pair.Value));
            }
        }

        return events;
    }

    public IReadOnlyList<LayerRoutedEvent> Reset()
    {
        var previousLayer = ActiveLayer;
        var events = new List<LayerRoutedEvent>();
        foreach (var combo in _activeCombos.ToArray())
        {
            events.AddRange(StopComboOutput(combo));
        }

        _activeCombos.Clear();
        _pendingCombo = null;
        foreach (var pair in _presses.OrderBy(pair => pair.Key))
        {
            var press = pair.Value;
            if (press.TapHoldState == TapHoldState.Hold &&
                press.TapHoldDefinition!.OnHold.Kind == Ack05ActionKind.KeyboardChord)
            {
                events.Add(Input(
                    pair.Key,
                    PhysicalEventType.Up,
                    press.Layer,
                    press.TapHoldDefinition.OnHold));
            }
            else if (press.TapHoldState == TapHoldState.None &&
                !IsLayerAction(press.Action.Kind))
            {
                events.Add(Input(pair.Key, PhysicalEventType.Up, press.Layer));
            }
        }

        _presses.Clear();
        _momentaryCounts.Clear();
        _toggledLayers.Clear();
        if (previousLayer != 0)
        {
            events.Add(LayerChange(Ack05Control.K1, 0));
        }

        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> RouteCombo(
        PhysicalInputEvent input,
        DateTimeOffset occurredAt)
    {
        if (_pendingCombo is null)
        {
            if (input.Type != PhysicalEventType.Down)
            {
                return RouteResolved(input, occurredAt);
            }

            var layer = ActiveLayer;
            var candidates = _combos
                .Where(combo => combo.Layers.Contains(layer) &&
                    combo.Controls.Contains(input.Control) &&
                    (!combo.Ordered || combo.Controls[0] == input.Control))
                .ToArray();
            if (candidates.Length == 0)
            {
                return RouteResolved(input, occurredAt);
            }

            _pendingCombo = new PendingCombo(
                input.Control,
                layer,
                occurredAt,
                candidates);
            _pendingCombo.Buffer.Add(new BufferedInput(input, occurredAt));
            _pendingCombo.DownControls.Add(input.Control);
            return [];
        }

        var pending = _pendingCombo;
        if (input.Type == PhysicalEventType.Down)
        {
            if (pending.DownControls.Contains(input.Control))
            {
                return [];
            }

            var matches = pending.Candidates.Where(combo =>
                combo.Controls.Contains(input.Control) &&
                occurredAt - pending.FirstAt <= TimeSpan.FromMilliseconds(TermFor(combo)) &&
                (!combo.Ordered || combo.Controls[1] == input.Control)).ToArray();
            if (matches.Length != 1)
            {
                var failed = new List<LayerRoutedEvent>(FailPendingCombo());
                failed.AddRange(RouteResolved(input, occurredAt));
                return failed;
            }

            pending.Buffer.Add(new BufferedInput(input, occurredAt));
            pending.DownControls.Add(input.Control);
            pending.Match = matches[0];
            pending.SecondAt = occurredAt;
            return [];
        }

        if (!pending.DownControls.Contains(input.Control))
        {
            var failed = new List<LayerRoutedEvent>(FailPendingCombo());
            failed.AddRange(RouteResolved(input, occurredAt));
            return failed;
        }

        pending.Buffer.Add(new BufferedInput(input, occurredAt));
        pending.DownControls.Remove(input.Control);
        if (pending.Match is null)
        {
            return FailPendingCombo();
        }

        if (pending.Match.Trigger == Ack05ComboTrigger.Press)
        {
            var started = new List<LayerRoutedEvent>(ConfirmPendingCombo());
            var active = _activeCombos.Last();
            started.AddRange(RouteActiveCombo(active, input));
            return started;
        }

        if (pending.Match.Trigger == Ack05ComboTrigger.HoldTogether)
        {
            return FailPendingCombo();
        }

        if (pending.DownControls.Count == 0)
        {
            var elapsed = occurredAt - pending.FirstAt;
            if (elapsed <= TimeSpan.FromMilliseconds(pending.Match.HoldTermMs!.Value))
            {
                var events = EmitComboPulse(pending.Match, pending.FirstControl, pending.Layer);
                _pendingCombo = null;
                return events;
            }

            return FailPendingCombo();
        }

        return [];
    }

    private IReadOnlyList<LayerRoutedEvent> TickCombo()
    {
        if (_pendingCombo is not { } pending)
        {
            return [];
        }

        var now = _now();
        if (pending.Match is null)
        {
            var longestTerm = pending.Candidates.Max(TermFor);
            return now - pending.FirstAt >= TimeSpan.FromMilliseconds(longestTerm)
                ? FailPendingCombo()
                : [];
        }

        if (pending.Match.Trigger == Ack05ComboTrigger.Press)
        {
            return ConfirmPendingCombo();
        }

        if (pending.Match.Trigger == Ack05ComboTrigger.TapOnly)
        {
            return now - pending.FirstAt >=
                TimeSpan.FromMilliseconds(pending.Match.HoldTermMs!.Value)
                ? FailPendingCombo()
                : [];
        }

        return now - pending.SecondAt >=
            TimeSpan.FromMilliseconds(pending.Match.HoldTermMs!.Value)
            ? ConfirmPendingCombo()
            : [];
    }

    private IReadOnlyList<LayerRoutedEvent> ConfirmPendingCombo()
    {
        var pending = _pendingCombo!;
        var definition = pending.Match!;
        var active = new ActiveCombo(
            definition,
            pending.FirstControl,
            pending.Layer,
            pending.DownControls);
        _activeCombos.Add(active);
        _pendingCombo = null;
        return StartComboOutput(active);
    }

    private IReadOnlyList<LayerRoutedEvent> FailPendingCombo()
    {
        if (_pendingCombo is not { } pending)
        {
            return [];
        }

        _pendingCombo = null;
        var events = new List<LayerRoutedEvent>();
        foreach (var buffered in pending.Buffer)
        {
            events.AddRange(RouteResolved(buffered.Input, buffered.OccurredAt));
        }

        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> RouteActiveCombo(
        ActiveCombo combo,
        PhysicalInputEvent input)
    {
        if (input.Type == PhysicalEventType.Down)
        {
            combo.DownControls.Add(input.Control);
            return [];
        }

        combo.DownControls.Remove(input.Control);
        var releaseNow = !combo.OutputReleased &&
            (combo.Definition.Release == Ack05ComboRelease.FirstMember ||
                combo.DownControls.Count == 0);
        var events = releaseNow ? StopComboOutput(combo) : [];
        if (combo.DownControls.Count == 0)
        {
            _activeCombos.Remove(combo);
        }

        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> StartComboOutput(ActiveCombo combo)
    {
        var action = combo.Definition.Action;
        return action.Kind switch
        {
            Ack05ActionKind.Disabled => [],
            Ack05ActionKind.KeyboardChord =>
                [Input(combo.Owner, PhysicalEventType.Down, combo.Layer, action)],
            Ack05ActionKind.MouseWheel =>
                [Input(combo.Owner, PhysicalEventType.Pulse, combo.Layer, action)],
            Ack05ActionKind.MomentaryLayer =>
                ChangeMomentary(combo.Owner, action.TargetLayer!.Value, 1),
            Ack05ActionKind.ToggleLayer =>
                ToggleLayer(combo.Owner, action.TargetLayer!.Value),
            _ => throw new InvalidOperationException($"Unsupported Combo action {action.Kind}."),
        };
    }

    private IReadOnlyList<LayerRoutedEvent> StopComboOutput(ActiveCombo combo)
    {
        if (combo.OutputReleased)
        {
            return [];
        }

        combo.OutputReleased = true;
        var action = combo.Definition.Action;
        return action.Kind switch
        {
            Ack05ActionKind.KeyboardChord =>
                [Input(combo.Owner, PhysicalEventType.Up, combo.Layer, action)],
            Ack05ActionKind.MomentaryLayer =>
                ChangeMomentary(combo.Owner, action.TargetLayer!.Value, -1),
            _ => [],
        };
    }

    private IReadOnlyList<LayerRoutedEvent> EmitComboPulse(
        Ack05ComboDefinition definition,
        Ack05Control owner,
        int layer)
    {
        var action = definition.Action;
        return action.Kind switch
        {
            Ack05ActionKind.Disabled => [],
            Ack05ActionKind.KeyboardChord or Ack05ActionKind.MouseWheel =>
                [Input(owner, PhysicalEventType.Pulse, layer, action)],
            Ack05ActionKind.MomentaryLayer =>
                [.. ChangeMomentary(owner, action.TargetLayer!.Value, 1),
                 .. ChangeMomentary(owner, action.TargetLayer.Value, -1)],
            Ack05ActionKind.ToggleLayer => ToggleLayer(owner, action.TargetLayer!.Value),
            _ => throw new InvalidOperationException($"Unsupported Combo action {action.Kind}."),
        };
    }

    private int TermFor(Ack05ComboDefinition definition) =>
        definition.TermMs ?? _comboDefaultTermMs;

    private IReadOnlyList<LayerRoutedEvent> RouteDown(
        Ack05Control control,
        DateTimeOffset occurredAt)
    {
        if (_presses.ContainsKey(control))
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        var resolved = Resolve(control);
        var press = new ActivePress(resolved.Layer, resolved.Action, occurredAt);
        if (resolved.Action.Kind == Ack05ActionKind.TapHold)
        {
            press.TapHoldDefinition = _tapHolds[resolved.Action.TapHoldId!];
            press.TapHoldState = TapHoldState.Pending;
        }

        _presses[control] = press;
        if (press.TapHoldState == TapHoldState.Pending)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        return ApplyLayerDown(control, press) ??
            new[] { Input(control, PhysicalEventType.Down, resolved.Layer) };
    }

    private IReadOnlyList<LayerRoutedEvent> RouteUp(Ack05Control control)
    {
        if (!_presses.Remove(control, out var press))
        {
            var resolved = Resolve(control);
            return IsLayerAction(resolved.Action.Kind)
                ? Array.Empty<LayerRoutedEvent>()
                : new[] { Input(control, PhysicalEventType.Up, resolved.Layer) };
        }

        if (press.TapHoldState == TapHoldState.Pending)
        {
            return ApplyTap(control, press);
        }

        if (press.TapHoldState == TapHoldState.Hold)
        {
            return ReleaseHold(control, press);
        }

        return ApplyLayerUp(control, press) ??
            new[] { Input(control, PhysicalEventType.Up, press.Layer) };
    }

    private IReadOnlyList<LayerRoutedEvent> ResolveTapHoldInterrupt(
        Ack05Control interruptingControl,
        bool isRelease)
    {
        var events = new List<LayerRoutedEvent>();
        foreach (var pair in _presses
            .Where(pair => pair.Key != interruptingControl &&
                pair.Value.TapHoldState == TapHoldState.Pending)
            .OrderBy(pair => pair.Value.PressedAt)
            .ToArray())
        {
            var press = pair.Value;
            var mode = press.TapHoldDefinition!.InterruptMode;
            if (!isRelease)
            {
                if (mode == Ack05TapHoldInterruptMode.HoldOnOtherKeyPress)
                {
                    events.AddRange(ConfirmTapHold(pair.Key, press));
                }
                else if (mode == Ack05TapHoldInterruptMode.HoldOnOtherKeyTap)
                {
                    press.InterruptingControls.Add(interruptingControl);
                }
            }
            else if (mode == Ack05TapHoldInterruptMode.HoldOnOtherKeyTap &&
                press.InterruptingControls.Remove(interruptingControl))
            {
                events.AddRange(ConfirmTapHold(pair.Key, press));
            }
        }

        return events;
    }

    private IReadOnlyList<LayerRoutedEvent> ConfirmTapHold(
        Ack05Control control,
        ActivePress press)
    {
        if (press.TapHoldState != TapHoldState.Pending)
        {
            return Array.Empty<LayerRoutedEvent>();
        }

        press.TapHoldState = TapHoldState.Hold;
        var action = press.TapHoldDefinition!.OnHold;
        return action.Kind switch
        {
            Ack05ActionKind.Disabled => Array.Empty<LayerRoutedEvent>(),
            Ack05ActionKind.KeyboardChord =>
                new[] { Input(control, PhysicalEventType.Down, press.Layer, action) },
            Ack05ActionKind.MomentaryLayer =>
                ChangeMomentary(control, action.TargetLayer!.Value, 1),
            _ => throw new InvalidOperationException(
                $"Unsupported Tap & Hold hold action {action.Kind}."),
        };
    }

    private IReadOnlyList<LayerRoutedEvent> ApplyTap(
        Ack05Control control,
        ActivePress press)
    {
        var action = press.TapHoldDefinition!.OnTap;
        return action.Kind switch
        {
            Ack05ActionKind.Disabled => Array.Empty<LayerRoutedEvent>(),
            Ack05ActionKind.KeyboardChord or Ack05ActionKind.MouseWheel =>
                new[] { Input(control, PhysicalEventType.Pulse, press.Layer, action) },
            Ack05ActionKind.ToggleLayer =>
                ToggleLayer(control, action.TargetLayer!.Value),
            _ => throw new InvalidOperationException(
                $"Unsupported Tap & Hold tap action {action.Kind}."),
        };
    }

    private IReadOnlyList<LayerRoutedEvent> ReleaseHold(
        Ack05Control control,
        ActivePress press)
    {
        var action = press.TapHoldDefinition!.OnHold;
        return action.Kind switch
        {
            Ack05ActionKind.Disabled => Array.Empty<LayerRoutedEvent>(),
            Ack05ActionKind.KeyboardChord =>
                new[] { Input(control, PhysicalEventType.Up, press.Layer, action) },
            Ack05ActionKind.MomentaryLayer =>
                ChangeMomentary(control, action.TargetLayer!.Value, -1),
            _ => throw new InvalidOperationException(
                $"Unsupported Tap & Hold hold action {action.Kind}."),
        };
    }

    private IReadOnlyList<LayerRoutedEvent> RoutePulse(Ack05Control control)
    {
        var resolved = Resolve(control);
        if (resolved.Action.Kind == Ack05ActionKind.ToggleLayer)
        {
            return ToggleLayer(control, resolved.Action.TargetLayer!.Value);
        }

        return IsLayerAction(resolved.Action.Kind)
            ? Array.Empty<LayerRoutedEvent>()
            : new[] { Input(control, PhysicalEventType.Pulse, resolved.Layer) };
    }

    private IReadOnlyList<LayerRoutedEvent>? ApplyLayerDown(
        Ack05Control control,
        ActivePress press)
    {
        var target = press.Action.TargetLayer;
        switch (press.Action.Kind)
        {
            case Ack05ActionKind.MomentaryLayer:
            case Ack05ActionKind.TapToggleHoldLayer:
                return ChangeMomentary(control, target!.Value, 1);
            case Ack05ActionKind.ToggleLayer:
                return ToggleLayer(control, target!.Value);
            default:
                return null;
        }
    }

    private IReadOnlyList<LayerRoutedEvent>? ApplyLayerUp(
        Ack05Control control,
        ActivePress press)
    {
        var target = press.Action.TargetLayer;
        switch (press.Action.Kind)
        {
            case Ack05ActionKind.MomentaryLayer:
                return ChangeMomentary(control, target!.Value, -1);
            case Ack05ActionKind.ToggleLayer:
                return Array.Empty<LayerRoutedEvent>();
            case Ack05ActionKind.TapToggleHoldLayer:
            {
                var before = ActiveLayer;
                AdjustMomentary(target!.Value, -1);
                var threshold = TimeSpan.FromMilliseconds(
                    press.Action.TapHoldThresholdMs ??
                    Ack05LayerSwitchSettings.DefaultTapHoldThresholdMs);
                if (!press.ChordUsed && _now() - press.PressedAt < threshold)
                {
                    ToggleState(target.Value);
                }

                return LayerChangeIfNeeded(control, before);
            }
            default:
                return null;
        }
    }

    private IReadOnlyList<LayerRoutedEvent> ChangeMomentary(
        Ack05Control control,
        int layer,
        int delta)
    {
        var before = ActiveLayer;
        AdjustMomentary(layer, delta);
        return LayerChangeIfNeeded(control, before);
    }

    private IReadOnlyList<LayerRoutedEvent> ToggleLayer(Ack05Control control, int layer)
    {
        var before = ActiveLayer;
        ToggleState(layer);
        return LayerChangeIfNeeded(control, before);
    }

    private void AdjustMomentary(int layer, int delta)
    {
        _momentaryCounts.TryGetValue(layer, out var count);
        count = Math.Max(0, count + delta);
        if (count == 0)
        {
            _momentaryCounts.Remove(layer);
        }
        else
        {
            _momentaryCounts[layer] = count;
        }
    }

    private void ToggleState(int layer)
    {
        if (!_toggledLayers.Remove(layer))
        {
            _toggledLayers.Add(layer);
        }
    }

    private IReadOnlyList<LayerRoutedEvent> LayerChangeIfNeeded(
        Ack05Control control,
        int previousLayer)
    {
        var active = ActiveLayer;
        return active == previousLayer
            ? Array.Empty<LayerRoutedEvent>()
            : new[] { LayerChange(control, active) };
    }

    private (int Layer, Ack05Action Action) Resolve(Ack05Control control)
    {
        foreach (var layer in ActiveLayers().OrderDescending())
        {
            var action = _bindings.TryGetValue((layer, control), out var configured)
                ? configured
                : layer == 0
                    ? Ack05Action.Disabled()
                    : Ack05Action.Transparent();
            if (action.Kind != Ack05ActionKind.Transparent)
            {
                return (layer, action);
            }
        }

        return (0, Ack05Action.Disabled());
    }

    private IEnumerable<int> ActiveLayers()
    {
        yield return 0;
        foreach (var layer in _toggledLayers)
        {
            if (layer > 0 && layer < _layerCount)
            {
                yield return layer;
            }
        }

        foreach (var pair in _momentaryCounts)
        {
            if (pair.Value > 0 && pair.Key > 0 && pair.Key < _layerCount)
            {
                yield return pair.Key;
            }
        }
    }

    private void MarkTapHoldChordUsed(Ack05Control inputControl)
    {
        foreach (var pair in _presses)
        {
            if (pair.Key != inputControl &&
                pair.Value.Action.Kind == Ack05ActionKind.TapToggleHoldLayer)
            {
                pair.Value.ChordUsed = true;
            }
        }
    }

    private static bool IsLayerAction(Ack05ActionKind kind) =>
        kind is Ack05ActionKind.MomentaryLayer or
            Ack05ActionKind.ToggleLayer or
            Ack05ActionKind.TapToggleHoldLayer;

    private static LayerRoutedEvent Input(
        Ack05Control control,
        PhysicalEventType type,
        int layer,
        Ack05Action? actionOverride = null) =>
        new(LayerRouteKind.Input, control, type, layer, actionOverride);

    private static LayerRoutedEvent LayerChange(Ack05Control control, int layer) =>
        new(LayerRouteKind.LayerChanged, control, PhysicalEventType.Pulse, layer);

    private sealed class ActivePress(
        int layer,
        Ack05Action action,
        DateTimeOffset pressedAt)
    {
        public int Layer { get; } = layer;
        public Ack05Action Action { get; } = action;
        public DateTimeOffset PressedAt { get; } = pressedAt;
        public bool ChordUsed { get; set; }
        public Ack05TapHoldDefinition? TapHoldDefinition { get; set; }
        public TapHoldState TapHoldState { get; set; }
        public HashSet<Ack05Control> InterruptingControls { get; } = [];
    }

    private sealed class PendingCombo(
        Ack05Control firstControl,
        int layer,
        DateTimeOffset firstAt,
        Ack05ComboDefinition[] candidates)
    {
        public Ack05Control FirstControl { get; } = firstControl;
        public int Layer { get; } = layer;
        public DateTimeOffset FirstAt { get; } = firstAt;
        public DateTimeOffset SecondAt { get; set; }
        public Ack05ComboDefinition[] Candidates { get; } = candidates;
        public Ack05ComboDefinition? Match { get; set; }
        public List<BufferedInput> Buffer { get; } = [];
        public HashSet<Ack05Control> DownControls { get; } = [];
    }

    private sealed class ActiveCombo(
        Ack05ComboDefinition definition,
        Ack05Control owner,
        int layer,
        IEnumerable<Ack05Control> downControls)
    {
        public Ack05ComboDefinition Definition { get; } = definition;
        public Ack05Control Owner { get; } = owner;
        public int Layer { get; } = layer;
        public HashSet<Ack05Control> DownControls { get; } = [.. downControls];
        public bool OutputReleased { get; set; }
    }

    private readonly record struct BufferedInput(
        PhysicalInputEvent Input,
        DateTimeOffset OccurredAt);

    private enum TapHoldState
    {
        None,
        Pending,
        Hold,
    }
}
