using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ack05Enhanced.Core;

public sealed class Ack05Configuration
{
    public const int CurrentSchemaVersion = 3;
    public const int MinimumLayerCount = 2;
    public const int DefaultLayerCount = 4;
    public const int MaximumLayerCount = 8;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string DefaultProfileId { get; init; } = "default";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ack05LayerSwitchSettings? LayerSwitch { get; init; }

    public Ack05Profile[] Profiles { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    public static Ack05Configuration CreateDefault() =>
        new()
        {
            Profiles =
            [
                new Ack05Profile
                {
                    Id = "default",
                    Name = "Default",
                    Layers =
                    [
                        CreateBaseLayer(),
                        .. Enumerable.Range(1, DefaultLayerCount - 1)
                            .Select(CreateTransparentLayer),
                    ],
                },
            ],
        };

    private static Ack05Layer CreateBaseLayer() =>
        new()
        {
            Index = 0,
            Bindings =
            [
                .. Enum.GetValues<Ack05Control>()
                    .Select(control => new Ack05Binding
                    {
                        Control = control,
                        Action = control == Ack05Control.K1
                            ? Ack05Action.MomentaryLayer(1)
                            : Ack05Action.Disabled(),
                    }),
            ],
        };

    private static Ack05Layer CreateTransparentLayer(int index) =>
        new()
        {
            Index = index,
            Bindings =
            [
                .. Enum.GetValues<Ack05Control>()
                    .Select(control => new Ack05Binding
                    {
                        Control = control,
                        Action = Ack05Action.Transparent(),
                    }),
            ],
        };
}

public enum Ack05LayerSwitchMode
{
    Momentary,
    Toggle,
    TapToggleHold,
}

public sealed class Ack05LayerSwitchSettings
{
    public const int MinimumTapHoldThresholdMs = 100;
    public const int MaximumTapHoldThresholdMs = 1000;
    public const int DefaultTapHoldThresholdMs = 200;

    public Ack05Control Control { get; init; } = Ack05Control.K1;

    public Ack05LayerSwitchMode Mode { get; init; } = Ack05LayerSwitchMode.Momentary;

    public int TapHoldThresholdMs { get; init; } = DefaultTapHoldThresholdMs;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class Ack05Profile
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public Ack05ApplicationMatch? ApplicationMatch { get; init; }

    public Ack05Layer[] Layers { get; init; } = [];

    public Ack05TapHoldDefinition[] TapHolds { get; init; } = [];

    public Ack05ComboDefaults ComboDefaults { get; init; } = new();

    public Ack05ComboDefinition[] Combos { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class Ack05ComboDefaults
{
    public const int MinimumTermMs = 20;
    public const int MaximumTermMs = 200;
    public const int DefaultTermMs = 50;

    public int TermMs { get; init; } = DefaultTermMs;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public enum Ack05ComboTrigger
{
    Press,
    TapOnly,
    HoldTogether,
}

public enum Ack05ComboRelease
{
    FirstMember,
    AllMembers,
}

public sealed class Ack05ComboDefinition
{
    public const int MinimumHoldTermMs = 50;
    public const int MaximumHoldTermMs = 2000;
    public const int DefaultHoldTermMs = 200;

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public Ack05Control[] Controls { get; init; } = [];

    public int[] Layers { get; init; } = [];

    public int? TermMs { get; init; }

    public bool Ordered { get; init; }

    public Ack05ComboTrigger Trigger { get; init; } = Ack05ComboTrigger.Press;

    public int? HoldTermMs { get; init; }

    public Ack05ComboRelease Release { get; init; } = Ack05ComboRelease.FirstMember;

    public Ack05Action Action { get; init; } = Ack05Action.Disabled();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public enum Ack05TapHoldInterruptMode
{
    HoldOnOtherKeyPress,
    HoldOnOtherKeyTap,
    TimeoutOnly,
}

public sealed class Ack05TapHoldDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int TappingTermMs { get; init; } = Ack05LayerSwitchSettings.DefaultTapHoldThresholdMs;

    public Ack05TapHoldInterruptMode InterruptMode { get; init; } =
        Ack05TapHoldInterruptMode.HoldOnOtherKeyPress;

    public Ack05Action OnTap { get; init; } = Ack05Action.Disabled();

    public Ack05Action OnHold { get; init; } = Ack05Action.Disabled();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class Ack05ApplicationMatch
{
    /// <summary>
    /// Executable file name without a path, for example "AfterFX.exe".
    /// Null means the profile has no application-specific match rule.
    /// </summary>
    public string? ProcessName { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class Ack05Layer
{
    public int Index { get; init; }

    public Ack05Binding[] Bindings { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class Ack05Binding
{
    public Ack05Control Control { get; init; }

    public Ack05Action Action { get; init; } = Ack05Action.Disabled();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
