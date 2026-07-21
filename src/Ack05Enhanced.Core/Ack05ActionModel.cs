using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ack05Enhanced.Core;

public enum Ack05ActionKind
{
    Disabled,
    Transparent,
    KeyboardChord,
    MouseWheel,
    MomentaryLayer,
    ToggleLayer,
    TapToggleHoldLayer,
    TapHold,
}

public sealed class Ack05Action
{
    public Ack05ActionKind Kind { get; init; }

    /// <summary>
    /// Key-down order for a keyboard chord. Output adapters release in reverse order.
    /// </summary>
    public Ack05KeyboardKey[]? Keys { get; init; }

    /// <summary>
    /// Signed wheel detents. Positive is up and negative is down.
    /// </summary>
    public int? WheelSteps { get; init; }

    /// <summary>
    /// Target layer for layer actions. Null for output actions.
    /// </summary>
    public int? TargetLayer { get; init; }

    /// <summary>
    /// Tap/hold threshold used only by TapToggleHoldLayer.
    /// </summary>
    public int? TapHoldThresholdMs { get; init; }

    /// <summary>
    /// Stable profile-owned definition ID used only by TapHold.
    /// </summary>
    public string? TapHoldId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    public static Ack05Action Disabled() => new() { Kind = Ack05ActionKind.Disabled };

    public static Ack05Action Transparent() => new() { Kind = Ack05ActionKind.Transparent };

    public static Ack05Action KeyboardChord(params Ack05KeyboardKey[] keys) =>
        new() { Kind = Ack05ActionKind.KeyboardChord, Keys = keys };

    public static Ack05Action MouseWheel(int steps) =>
        new() { Kind = Ack05ActionKind.MouseWheel, WheelSteps = steps };

    public static Ack05Action MomentaryLayer(int layer) =>
        new() { Kind = Ack05ActionKind.MomentaryLayer, TargetLayer = layer };

    public static Ack05Action ToggleLayer(int layer) =>
        new() { Kind = Ack05ActionKind.ToggleLayer, TargetLayer = layer };

    public static Ack05Action TapToggleHoldLayer(
        int layer,
        int thresholdMs = Ack05LayerSwitchSettings.DefaultTapHoldThresholdMs) =>
        new()
        {
            Kind = Ack05ActionKind.TapToggleHoldLayer,
            TargetLayer = layer,
            TapHoldThresholdMs = thresholdMs,
        };

    public static Ack05Action TapHold(string definitionId) =>
        new() { Kind = Ack05ActionKind.TapHold, TapHoldId = definitionId };
}
