namespace Ack05Enhanced.Core;

public static class Ack05ConfigurationMigrator
{
    public static Ack05Configuration MigrateV2(Ack05Configuration legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        if (legacy.SchemaVersion != 2)
        {
            throw new Ack05ConfigurationException(
                $"Cannot migrate schemaVersion {legacy.SchemaVersion} as v2.",
                new InvalidOperationException());
        }

        return new Ack05Configuration
        {
            SchemaVersion = Ack05Configuration.CurrentSchemaVersion,
            DefaultProfileId = legacy.DefaultProfileId,
            LayerSwitch = null,
            Profiles = legacy.Profiles.Select(profile => new Ack05Profile
            {
                Id = profile.Id,
                Name = profile.Name,
                ApplicationMatch = profile.ApplicationMatch,
                Layers = profile.Layers,
                TapHolds = [],
                ComboDefaults = new Ack05ComboDefaults(),
                Combos = [],
                ExtensionData = profile.ExtensionData,
            }).ToArray(),
            ExtensionData = legacy.ExtensionData,
        };
    }

    public static Ack05Configuration MigrateV1(Ack05Configuration legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        if (legacy.SchemaVersion != 1)
        {
            throw new Ack05ConfigurationException(
                $"Cannot migrate schemaVersion {legacy.SchemaVersion} as v1.",
                new InvalidOperationException());
        }

        var layerSwitch = legacy.LayerSwitch ?? new Ack05LayerSwitchSettings();
        var profiles = legacy.Profiles.Select(profile =>
            MigrateProfile(profile, layerSwitch)).ToArray();
        return new Ack05Configuration
        {
            SchemaVersion = Ack05Configuration.CurrentSchemaVersion,
            DefaultProfileId = legacy.DefaultProfileId,
            LayerSwitch = null,
            Profiles = profiles,
            ExtensionData = legacy.ExtensionData,
        };
    }

    private static Ack05Profile MigrateProfile(
        Ack05Profile profile,
        Ack05LayerSwitchSettings layerSwitch)
    {
        if (profile.Layers.Length != 2 ||
            profile.Layers.Select(layer => layer.Index).Distinct().Order().SequenceEqual([0, 1]) is false)
        {
            throw new Ack05ConfigurationException(
                $"profile '{profile.Id}': schema v1 migration requires exactly Layer 0 and Layer 1.",
                new InvalidOperationException());
        }

        var layer0 = profile.Layers.Single(layer => layer.Index == 0);
        var layer1 = profile.Layers.Single(layer => layer.Index == 1);
        return new Ack05Profile
        {
            Id = profile.Id,
            Name = profile.Name,
            ApplicationMatch = profile.ApplicationMatch,
            Layers =
            [
                MigrateLegacyLayer(layer0, layerSwitch, isBase: true),
                MigrateLegacyLayer(layer1, layerSwitch, isBase: false),
                CreateTransparentLayer(2),
                CreateTransparentLayer(3),
            ],
            TapHolds = [],
            ComboDefaults = new Ack05ComboDefaults(),
            Combos = [],
            ExtensionData = profile.ExtensionData,
        };
    }

    private static Ack05Layer MigrateLegacyLayer(
        Ack05Layer layer,
        Ack05LayerSwitchSettings layerSwitch,
        bool isBase)
    {
        var existing = layer.Bindings.ToDictionary(binding => binding.Control);
        var bindings = Enum.GetValues<Ack05Control>()
            .Select(control =>
            {
                existing.TryGetValue(control, out var binding);
                var action = control == layerSwitch.Control
                    ? isBase
                        ? LegacyLayerAction(layerSwitch)
                        : Ack05Action.Transparent()
                    : binding?.Action ?? Ack05Action.Disabled();
                return new Ack05Binding
                {
                    Control = control,
                    Action = action,
                    ExtensionData = binding?.ExtensionData,
                };
            })
            .ToArray();
        return new Ack05Layer
        {
            Index = layer.Index,
            Bindings = bindings,
            ExtensionData = layer.ExtensionData,
        };
    }

    private static Ack05Action LegacyLayerAction(Ack05LayerSwitchSettings layerSwitch) =>
        layerSwitch.Mode switch
        {
            Ack05LayerSwitchMode.Momentary => Ack05Action.MomentaryLayer(1),
            Ack05LayerSwitchMode.Toggle => Ack05Action.ToggleLayer(1),
            Ack05LayerSwitchMode.TapToggleHold => Ack05Action.TapToggleHoldLayer(
                1,
                layerSwitch.TapHoldThresholdMs),
            _ => throw new Ack05ConfigurationException(
                "layerSwitch.mode is unsupported for schema v1 migration.",
                new InvalidOperationException()),
        };

    private static Ack05Layer CreateTransparentLayer(int index) =>
        new()
        {
            Index = index,
            Bindings =
            [
                .. Enum.GetValues<Ack05Control>().Select(control => new Ack05Binding
                {
                    Control = control,
                    Action = Ack05Action.Transparent(),
                }),
            ],
        };
}
