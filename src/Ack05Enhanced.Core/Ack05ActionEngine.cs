namespace Ack05Enhanced.Core;

/// <summary>
/// Resolves routed physical inputs into logical output events. It owns no OS APIs.
/// </summary>
public sealed class Ack05ActionEngine
{
    private readonly Dictionary<(int Layer, Ack05Control Control), Ack05Action> _bindings = new();
    private readonly Dictionary<Ack05Control, ActiveChord> _activeChords = new();
    private readonly Dictionary<Ack05KeyboardKey, int> _keyReferenceCounts = new();
    private long _activationSequence;

    public Ack05ActionEngine(Ack05Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
        BuildBindings(profile);
    }

    public Ack05Profile Profile { get; private set; }

    public IReadOnlyList<Ack05OutputEvent> Apply(LayerRoutedEvent input)
    {
        if (input.Kind != LayerRouteKind.Input)
        {
            return [];
        }

        if (input.InputType == PhysicalEventType.Up)
        {
            return ReleaseControl(input.Control);
        }

        var action = input.ActionOverride;
        if (action is null &&
            !_bindings.TryGetValue((input.Layer, input.Control), out action))
        {
            return [];
        }

        if (action.Kind == Ack05ActionKind.Disabled)
        {
            return [];
        }

        return action.Kind switch
        {
            Ack05ActionKind.KeyboardChord => input.InputType == PhysicalEventType.Pulse
                ? TapChord(action.Keys!)
                : PressChord(input.Control, action.Keys!),
            Ack05ActionKind.MouseWheel =>
                [Ack05OutputEvent.MouseWheel(action.WheelSteps!.Value)],
            Ack05ActionKind.OpenTarget =>
                [Ack05OutputEvent.OpenTarget(action.Target!)],
            _ => [],
        };
    }

    /// <summary>
    /// Releases active chords before replacing the profile. The caller must deliver
    /// every returned event before processing more input.
    /// </summary>
    public IReadOnlyList<Ack05OutputEvent> ChangeProfile(Ack05Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var release = Reset();
        Profile = profile;
        BuildBindings(profile);
        return release;
    }

    public IReadOnlyList<Ack05OutputEvent> Reset()
    {
        var output = new List<Ack05OutputEvent>();
        foreach (var control in _activeChords
            .OrderByDescending(pair => pair.Value.Sequence)
            .Select(pair => pair.Key)
            .ToArray())
        {
            output.AddRange(ReleaseControl(control));
        }

        _activeChords.Clear();
        _keyReferenceCounts.Clear();
        return output;
    }

    private IReadOnlyList<Ack05OutputEvent> PressChord(
        Ack05Control control,
        Ack05KeyboardKey[] keys)
    {
        if (_activeChords.ContainsKey(control))
        {
            return [];
        }

        _activeChords.Add(control, new ActiveChord([.. keys], ++_activationSequence));
        return AddKeyReferences(keys);
    }

    private IReadOnlyList<Ack05OutputEvent> TapChord(Ack05KeyboardKey[] keys)
    {
        var output = new List<Ack05OutputEvent>();
        output.AddRange(AddKeyReferences(keys));
        output.AddRange(RemoveKeyReferences(keys));
        return output;
    }

    private IReadOnlyList<Ack05OutputEvent> ReleaseControl(Ack05Control control)
    {
        if (!_activeChords.Remove(control, out var chord))
        {
            return [];
        }

        return RemoveKeyReferences(chord.Keys);
    }

    private IReadOnlyList<Ack05OutputEvent> AddKeyReferences(
        IReadOnlyList<Ack05KeyboardKey> keys)
    {
        var output = new List<Ack05OutputEvent>();
        foreach (var key in keys)
        {
            _keyReferenceCounts.TryGetValue(key, out var count);
            _keyReferenceCounts[key] = count + 1;
            if (count == 0)
            {
                output.Add(Ack05OutputEvent.KeyDown(key));
            }
        }

        return output;
    }

    private IReadOnlyList<Ack05OutputEvent> RemoveKeyReferences(
        IReadOnlyList<Ack05KeyboardKey> keys)
    {
        var output = new List<Ack05OutputEvent>();
        for (var index = keys.Count - 1; index >= 0; index--)
        {
            var key = keys[index];
            if (!_keyReferenceCounts.TryGetValue(key, out var count))
            {
                continue;
            }

            if (count > 1)
            {
                _keyReferenceCounts[key] = count - 1;
            }
            else
            {
                _keyReferenceCounts.Remove(key);
                output.Add(Ack05OutputEvent.KeyUp(key));
            }
        }

        return output;
    }

    private void BuildBindings(Ack05Profile profile)
    {
        _bindings.Clear();
        foreach (var layer in profile.Layers)
        {
            foreach (var binding in layer.Bindings)
            {
                if (!_bindings.TryAdd((layer.Index, binding.Control), binding.Action))
                {
                    throw new ArgumentException(
                        $"Duplicate binding for Layer {layer.Index} {binding.Control}.",
                        nameof(profile));
                }
            }
        }
    }

    private sealed record ActiveChord(Ack05KeyboardKey[] Keys, long Sequence);
}
