using System.Text.RegularExpressions;

namespace Ack05Enhanced.Core;

public static partial class Ack05ConfigurationValidator
{
    private const int MaximumChordLength = 8;
    private const int MaximumWheelSteps = 10;

    public static IReadOnlyList<string> Validate(Ack05Configuration? configuration)
    {
        var errors = new List<string>();
        if (configuration is null)
        {
            errors.Add("Configuration is required.");
            return errors;
        }

        if (configuration.SchemaVersion != Ack05Configuration.CurrentSchemaVersion)
        {
            errors.Add(
                $"Unsupported schemaVersion {configuration.SchemaVersion}; expected {Ack05Configuration.CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(configuration.DefaultProfileId))
        {
            errors.Add("defaultProfileId is required.");
        }

        if (configuration.LayerSwitch is not null)
        {
            errors.Add("layerSwitch is obsolete in schema v3; assign layer actions to individual controls.");
        }

        if (configuration.Profiles is null || configuration.Profiles.Length == 0)
        {
            errors.Add("At least one profile is required.");
            return errors;
        }

        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var applicationMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in configuration.Profiles)
        {
            ValidateProfile(profile, profileIds, applicationMatches, errors);
        }

        if (!string.IsNullOrWhiteSpace(configuration.DefaultProfileId) &&
            !profileIds.Contains(configuration.DefaultProfileId))
        {
            errors.Add("defaultProfileId must identify an existing profile.");
        }

        return errors;
    }

    public static void ValidateOrThrow(Ack05Configuration? configuration)
    {
        var errors = Validate(configuration);
        if (errors.Count > 0)
        {
            throw new Ack05ConfigurationException(errors);
        }
    }

    private static void ValidateProfile(
        Ack05Profile? profile,
        HashSet<string> profileIds,
        HashSet<string> applicationMatches,
        List<string> errors)
    {
        if (profile is null)
        {
            errors.Add("profiles must not contain null entries.");
            return;
        }

        var path = string.IsNullOrWhiteSpace(profile.Id) ? "profile" : $"profile '{profile.Id}'";
        if (string.IsNullOrWhiteSpace(profile.Id) || !ProfileIdPattern().IsMatch(profile.Id))
        {
            errors.Add($"{path}: id must use lowercase letters, digits, '.', '_' or '-'.");
        }
        else if (!profileIds.Add(profile.Id))
        {
            errors.Add($"{path}: duplicate profile id.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add($"{path}: name is required.");
        }

        ValidateApplicationMatch(
            profile.ApplicationMatch,
            path,
            applicationMatches,
            errors);

        if (profile.Layers is null)
        {
            errors.Add($"{path}: layers are required.");
            return;
        }

        if (profile.Layers.Length is < Ack05Configuration.MinimumLayerCount or
            > Ack05Configuration.MaximumLayerCount)
        {
            errors.Add(
                $"{path}: requires {Ack05Configuration.MinimumLayerCount} to " +
                $"{Ack05Configuration.MaximumLayerCount} layers.");
        }

        var tapHolds = ValidateTapHolds(profile, path, profile.Layers.Length, errors);
        ValidateCombos(profile, path, profile.Layers.Length, errors);
        var layerIndices = new HashSet<int>();
        foreach (var layer in profile.Layers)
        {
            ValidateLayer(layer, path, profile.Layers.Length, layerIndices, tapHolds, errors);
        }

        if (!layerIndices.SetEquals(Enumerable.Range(0, profile.Layers.Length)))
        {
            errors.Add($"{path}: layer indices must be contiguous and start at 0.");
        }
    }

    private static void ValidateCombos(
        Ack05Profile profile,
        string profilePath,
        int layerCount,
        List<string> errors)
    {
        if (profile.ComboDefaults is null)
        {
            errors.Add($"{profilePath}: comboDefaults are required.");
        }
        else if (profile.ComboDefaults.TermMs is
            < Ack05ComboDefaults.MinimumTermMs or > Ack05ComboDefaults.MaximumTermMs)
        {
            errors.Add(
                $"{profilePath}: comboDefaults.termMs must be from " +
                $"{Ack05ComboDefaults.MinimumTermMs} to {Ack05ComboDefaults.MaximumTermMs}.");
        }

        if (profile.Combos is null)
        {
            errors.Add($"{profilePath}: combos are required.");
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validDefinitions = new List<Ack05ComboDefinition>();
        foreach (var definition in profile.Combos)
        {
            if (definition is null)
            {
                errors.Add($"{profilePath}: combos must not contain null entries.");
                continue;
            }

            var path = $"{profilePath}, combo '{definition.Id}'";
            if (string.IsNullOrWhiteSpace(definition.Id) ||
                !ProfileIdPattern().IsMatch(definition.Id))
            {
                errors.Add($"{path}: id must use lowercase letters, digits, '.', '_' or '-'.");
            }
            else if (!ids.Add(definition.Id))
            {
                errors.Add($"{path}: duplicate definition id.");
            }

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                errors.Add($"{path}: name is required.");
            }

            if (definition.Controls is null ||
                definition.Controls.Length != 2 ||
                definition.Controls.Distinct().Count() != 2 ||
                definition.Controls.Any(control => !Enum.IsDefined(control) ||
                    control is Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise))
            {
                errors.Add($"{path}: controls must contain exactly two distinct held controls (K1-K10 or RollerPress).");
            }

            if (definition.Layers is null ||
                definition.Layers.Length == 0 ||
                definition.Layers.Distinct().Count() != definition.Layers.Length ||
                definition.Layers.Any(layer => layer < 0 || layer >= layerCount))
            {
                errors.Add($"{path}: layers must contain unique existing layer indices.");
            }

            if (definition.TermMs is { } term &&
                term is < Ack05ComboDefaults.MinimumTermMs or > Ack05ComboDefaults.MaximumTermMs)
            {
                errors.Add(
                    $"{path}: termMs must be null or from {Ack05ComboDefaults.MinimumTermMs} " +
                    $"to {Ack05ComboDefaults.MaximumTermMs}.");
            }

            if (!Enum.IsDefined(definition.Trigger))
            {
                errors.Add($"{path}: unsupported trigger.");
            }

            if (!Enum.IsDefined(definition.Release))
            {
                errors.Add($"{path}: unsupported release policy.");
            }

            if (definition.Trigger == Ack05ComboTrigger.Press)
            {
                if (definition.HoldTermMs is not null)
                {
                    errors.Add($"{path}: holdTermMs is only valid for TapOnly or HoldTogether.");
                }
            }
            else if (definition.HoldTermMs is not { } holdTerm ||
                holdTerm is < Ack05ComboDefinition.MinimumHoldTermMs or
                    > Ack05ComboDefinition.MaximumHoldTermMs)
            {
                errors.Add(
                    $"{path}: holdTermMs must be from {Ack05ComboDefinition.MinimumHoldTermMs} " +
                    $"to {Ack05ComboDefinition.MaximumHoldTermMs} for {definition.Trigger}.");
            }

            if (definition.Action is null || definition.Action.Kind is not (
                Ack05ActionKind.Disabled or Ack05ActionKind.KeyboardChord or
                Ack05ActionKind.MouseWheel or Ack05ActionKind.MomentaryLayer or
                Ack05ActionKind.ToggleLayer))
            {
                errors.Add($"{path}: action is not supported as Combo output.");
            }
            else
            {
                ValidateAction(
                    definition.Action,
                    definition.Controls?.FirstOrDefault() ?? Ack05Control.K1,
                    layerIndex: -1,
                    layerCount,
                    $"{path}, action",
                    new Dictionary<string, Ack05TapHoldDefinition>(),
                    errors);
            }

            if (definition.Controls?.Length == 2 && definition.Layers?.Length > 0)
            {
                foreach (var previous in validDefinitions)
                {
                    if (previous.Controls.Order().SequenceEqual(definition.Controls.Order()) &&
                        previous.Layers.Intersect(definition.Layers).Any())
                    {
                        errors.Add(
                            $"{path}: another Combo uses the same key pair on an overlapping layer.");
                        break;
                    }
                }

                validDefinitions.Add(definition);
            }
        }
    }

    private static IReadOnlyDictionary<string, Ack05TapHoldDefinition> ValidateTapHolds(
        Ack05Profile profile,
        string profilePath,
        int layerCount,
        List<string> errors)
    {
        if (profile.TapHolds is null)
        {
            errors.Add($"{profilePath}: tapHolds are required.");
            return new Dictionary<string, Ack05TapHoldDefinition>();
        }

        var definitions = new Dictionary<string, Ack05TapHoldDefinition>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var definition in profile.TapHolds)
        {
            if (definition is null)
            {
                errors.Add($"{profilePath}: tapHolds must not contain null entries.");
                continue;
            }

            var path = $"{profilePath}, tapHold '{definition.Id}'";
            if (string.IsNullOrWhiteSpace(definition.Id) ||
                !ProfileIdPattern().IsMatch(definition.Id))
            {
                errors.Add($"{path}: id must use lowercase letters, digits, '.', '_' or '-'.");
            }
            else if (!definitions.TryAdd(definition.Id, definition))
            {
                errors.Add($"{path}: duplicate definition id.");
            }

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                errors.Add($"{path}: name is required.");
            }

            if (definition.TappingTermMs is
                < Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs or
                > Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs)
            {
                errors.Add(
                    $"{path}: tappingTermMs must be from " +
                    $"{Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs} to " +
                    $"{Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs}.");
            }

            if (!Enum.IsDefined(definition.InterruptMode))
            {
                errors.Add($"{path}: unsupported interruptMode.");
            }

            ValidateTapHoldSlot(
                definition.OnTap,
                isHold: false,
                layerCount,
                $"{path}, onTap",
                errors);
            ValidateTapHoldSlot(
                definition.OnHold,
                isHold: true,
                layerCount,
                $"{path}, onHold",
                errors);
        }

        return definitions;
    }

    private static void ValidateTapHoldSlot(
        Ack05Action? action,
        bool isHold,
        int layerCount,
        string path,
        List<string> errors)
    {
        if (action is null)
        {
            errors.Add($"{path}: action is required.");
            return;
        }

        var allowed = isHold
            ? action.Kind is Ack05ActionKind.Disabled or
                Ack05ActionKind.KeyboardChord or
                Ack05ActionKind.MomentaryLayer
            : action.Kind is Ack05ActionKind.Disabled or
                Ack05ActionKind.KeyboardChord or
                Ack05ActionKind.MouseWheel or
                Ack05ActionKind.ToggleLayer;
        if (!allowed)
        {
            errors.Add(
                $"{path}: {action.Kind} is not supported in the {(isHold ? "Hold" : "Tap")} slot.");
            return;
        }

        ValidateAction(
            action,
            Ack05Control.K1,
            layerIndex: -1,
            layerCount,
            path,
            new Dictionary<string, Ack05TapHoldDefinition>(),
            errors);
    }

    private static void ValidateApplicationMatch(
        Ack05ApplicationMatch? match,
        string path,
        HashSet<string> applicationMatches,
        List<string> errors)
    {
        if (match is null || match.ProcessName is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(match.ProcessName) ||
            match.ProcessName.IndexOfAny(['/', '\\']) >= 0)
        {
            errors.Add($"{path}: applicationMatch.processName must be a file name without a path.");
        }
        else if (!applicationMatches.Add(
            Ack05ProfileSelector.NormalizeProcessName(match.ProcessName)))
        {
            errors.Add($"{path}: duplicate applicationMatch.processName.");
        }
    }

    private static void ValidateLayer(
        Ack05Layer? layer,
        string profilePath,
        int layerCount,
        HashSet<int> layerIndices,
        IReadOnlyDictionary<string, Ack05TapHoldDefinition> tapHolds,
        List<string> errors)
    {
        if (layer is null)
        {
            errors.Add($"{profilePath}: layers must not contain null entries.");
            return;
        }

        var path = $"{profilePath}, layer {layer.Index}";
        if (layer.Index < 0 || layer.Index >= layerCount)
        {
            errors.Add($"{path}: layer index is outside the configured layer range.");
        }

        if (!layerIndices.Add(layer.Index))
        {
            errors.Add($"{path}: duplicate layer index.");
        }

        if (layer.Bindings is null)
        {
            errors.Add($"{path}: bindings are required.");
            return;
        }

        var controls = new HashSet<Ack05Control>();
        foreach (var binding in layer.Bindings)
        {
            ValidateBinding(binding, path, layer.Index, layerCount, controls, tapHolds, errors);
        }
    }

    private static void ValidateBinding(
        Ack05Binding? binding,
        string layerPath,
        int layerIndex,
        int layerCount,
        HashSet<Ack05Control> controls,
        IReadOnlyDictionary<string, Ack05TapHoldDefinition> tapHolds,
        List<string> errors)
    {
        if (binding is null)
        {
            errors.Add($"{layerPath}: bindings must not contain null entries.");
            return;
        }

        var path = $"{layerPath}, binding {binding.Control}";
        if (!Enum.IsDefined(binding.Control))
        {
            errors.Add($"{path}: unsupported control.");
        }
        if (!controls.Add(binding.Control))
        {
            errors.Add($"{path}: duplicate binding.");
        }

        ValidateAction(binding.Action, binding.Control, layerIndex, layerCount, path, tapHolds, errors);
    }

    private static void ValidateAction(
        Ack05Action? action,
        Ack05Control control,
        int layerIndex,
        int layerCount,
        string path,
        IReadOnlyDictionary<string, Ack05TapHoldDefinition> tapHolds,
        List<string> errors)
    {
        if (action is null)
        {
            errors.Add($"{path}: action is required.");
            return;
        }

        if (!Enum.IsDefined(action.Kind))
        {
            errors.Add($"{path}: unsupported action kind.");
            return;
        }

        switch (action.Kind)
        {
            case Ack05ActionKind.Disabled:
            case Ack05ActionKind.Transparent:
                if (action.Keys is not null ||
                    action.WheelSteps is not null ||
                    action.TargetLayer is not null ||
                    action.TapHoldThresholdMs is not null ||
                    action.TapHoldId is not null)
                {
                    errors.Add($"{path}: {action.Kind} action must not contain a payload.");
                }

                break;

            case Ack05ActionKind.KeyboardChord:
                if (action.Keys is null ||
                    action.Keys.Length == 0 ||
                    action.Keys.Length > MaximumChordLength)
                {
                    errors.Add($"{path}: keyboardChord requires 1 to {MaximumChordLength} keys.");
                }
                else
                {
                    if (action.Keys.Any(key => !Enum.IsDefined(key)))
                    {
                        errors.Add($"{path}: keyboardChord contains an unsupported key.");
                    }

                    if (action.Keys.Distinct().Count() != action.Keys.Length)
                    {
                        errors.Add($"{path}: keyboardChord keys must be unique.");
                    }
                }

                if (action.WheelSteps is not null ||
                    action.TargetLayer is not null ||
                    action.TapHoldThresholdMs is not null ||
                    action.TapHoldId is not null)
                {
                    errors.Add($"{path}: keyboardChord contains an unsupported payload.");
                }

                break;

            case Ack05ActionKind.MouseWheel:
                if (action.Keys is not null ||
                    action.TargetLayer is not null ||
                    action.TapHoldThresholdMs is not null ||
                    action.TapHoldId is not null)
                {
                    errors.Add($"{path}: mouseWheel contains an unsupported payload.");
                }

                if (action.WheelSteps is null ||
                    action.WheelSteps == 0 ||
                    Math.Abs(action.WheelSteps.Value) > MaximumWheelSteps)
                {
                    errors.Add(
                        $"{path}: mouseWheel requires wheelSteps from -{MaximumWheelSteps} to {MaximumWheelSteps}, excluding zero.");
                }

                break;

            case Ack05ActionKind.MomentaryLayer:
            case Ack05ActionKind.ToggleLayer:
            case Ack05ActionKind.TapToggleHoldLayer:
                if (action.Keys is not null ||
                    action.WheelSteps is not null ||
                    action.TapHoldId is not null)
                {
                    errors.Add($"{path}: layer action must not contain keys or wheelSteps.");
                }

                if (action.TargetLayer is null ||
                    action.TargetLayer < 0 ||
                    action.TargetLayer >= layerCount)
                {
                    errors.Add($"{path}: layer action targetLayer must identify an existing layer.");
                }
                else if (action.TargetLayer == layerIndex)
                {
                    errors.Add($"{path}: layer action cannot target its own layer.");
                }

                if (action.Kind == Ack05ActionKind.TapToggleHoldLayer)
                {
                    if (action.TapHoldThresholdMs is
                        < Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs or
                        > Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs)
                    {
                        errors.Add(
                            $"{path}: tapToggleHoldLayer.tapHoldThresholdMs must be from " +
                            $"{Ack05LayerSwitchSettings.MinimumTapHoldThresholdMs} to " +
                            $"{Ack05LayerSwitchSettings.MaximumTapHoldThresholdMs}.");
                    }
                }
                else if (action.TapHoldThresholdMs is not null)
                {
                    errors.Add($"{path}: only tapToggleHoldLayer accepts tapHoldThresholdMs.");
                }

                if (control is Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise &&
                    action.Kind is not Ack05ActionKind.ToggleLayer)
                {
                    errors.Add($"{path}: pulse-only roller controls support ToggleLayer only.");
                }

                break;

            case Ack05ActionKind.TapHold:
                if (action.Keys is not null ||
                    action.WheelSteps is not null ||
                    action.TargetLayer is not null ||
                    action.TapHoldThresholdMs is not null)
                {
                    errors.Add($"{path}: tapHold contains an unsupported payload.");
                }

                if (string.IsNullOrWhiteSpace(action.TapHoldId) ||
                    !tapHolds.TryGetValue(action.TapHoldId, out var definition))
                {
                    errors.Add($"{path}: tapHoldId must identify an existing definition.");
                    break;
                }

                if (control is Ack05Control.RollerClockwise or Ack05Control.RollerCounterClockwise)
                {
                    errors.Add($"{path}: pulse-only roller controls do not support Tap & Hold.");
                }

                if (definition.OnTap.TargetLayer == layerIndex ||
                    definition.OnHold.TargetLayer == layerIndex)
                {
                    errors.Add($"{path}: referenced Tap & Hold cannot target its own binding layer.");
                }

                break;
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileIdPattern();
}

public sealed class Ack05ConfigurationException : Exception
{
    public Ack05ConfigurationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public Ack05ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [message];
    }

    public IReadOnlyList<string> Errors { get; }
}
