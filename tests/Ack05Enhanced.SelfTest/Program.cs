using Ack05Enhanced.Core;
using Ack05Enhanced.Windows;
using System.Threading.Channels;
using System.Text.Json;

var tests = new (string Name, Action Run)[]
{
    ("raw report decoder", RawReportDecoder),
    ("all key bit mapping", AllKeyBitMapping),
    ("malformed report rejection", MalformedReportRejection),
    ("physical transition tracking", PhysicalTransitionTracking),
    ("momentary layer routing", MomentaryLayerRouting),
    ("configurable layer control routing", ConfigurableLayerControlRouting),
    ("toggle layer routing", ToggleLayerRouting),
    ("tap-toggle hold layer routing", TapToggleHoldLayerRouting),
    ("Vial-style keymap layer routing", KeymapLayerRouting),
    ("transparent and layer precedence", TransparentAndLayerPrecedence),
    ("keymap tap-toggle hold layer", KeymapTapToggleHoldLayer),
    ("Tap & Hold configuration round trip", TapHoldConfigurationRoundTrip),
    ("Tap & Hold validation", TapHoldValidation),
    ("Tap & Hold keyboard lifecycle", TapHoldKeyboardLifecycle),
    ("Tap & Hold layer interrupt routing", TapHoldLayerInterruptRouting),
    ("Tap & Hold reset safety", TapHoldResetSafety),
    ("Combo configuration and validation", ComboConfigurationAndValidation),
    ("Combo press lifecycle and fallback", ComboPressLifecycleAndFallback),
    ("Combo trigger modes", ComboTriggerModes),
    ("Combo reset safety", ComboResetSafety),
    ("Combo momentary layer release policy", ComboMomentaryLayerReleasePolicy),
    ("input session reset", InputSessionReset),
    ("reconnect service reset", ReconnectServiceReset),
    ("disconnect reset", DisconnectReset),
    ("default configuration round trip", DefaultConfigurationRoundTrip),
    ("action configuration round trip", ActionConfigurationRoundTrip),
    ("unknown configuration fields round trip", UnknownConfigurationFieldsRoundTrip),
    ("future configuration schema rejection", FutureConfigurationSchemaRejection),
    ("invalid configuration rejection", InvalidConfigurationRejection),
    ("layer action validation", LayerActionValidation),
    ("action engine chord lifecycle", ActionEngineChordLifecycle),
    ("action engine pulse and reset", ActionEnginePulseAndReset),
    ("SendInput safety registry", SendInputSafetyRegistry),
    ("SendInput best-effort panic release", SendInputBestEffortPanicRelease),
    ("application profile selection", ApplicationProfileSelection),
    ("live runtime lifecycle", () => LiveRuntimeLifecycle().GetAwaiter().GetResult()),
    ("live runtime configuration update", () => LiveRuntimeConfigurationUpdate().GetAwaiter().GetResult()),
    ("live runtime keymap layers", () => LiveRuntimeKeymapLayers().GetAwaiter().GetResult()),
    ("live runtime Tap & Hold timer", () => LiveRuntimeTapHoldTimer().GetAwaiter().GetResult()),
    ("live runtime Combo timer", () => LiveRuntimeComboTimer().GetAwaiter().GetResult()),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static void RawReportDecoder()
{
    var report = new byte[] { 0x02, 0xF0, 0x03, 0x06, 0, 0, 0, 0x01 };
    Assert(
        Ack05RawReportDecoder.TryDecode(report, out var snapshot, out var error),
        error ?? "decode failed");
    Assert(snapshot.IsHeld(Ack05Button.K1), "K1 should be held");
    Assert(snapshot.IsHeld(Ack05Button.K2), "K2 should be held");
    Assert(snapshot.IsHeld(Ack05Button.K10), "K10 should be held");
    Assert(snapshot.IsHeld(Ack05Button.RollerPress), "roller press should be held");
    Assert(!snapshot.IsHeld(Ack05Button.K9), "K9 should not be held");
    Assert(snapshot.Roller == RollerPulse.Clockwise, "roller should be clockwise");

    var counterClockwise = new byte[] { 0x02, 0xF0, 0, 0, 0, 0, 0, 0xFF };
    Assert(
        Ack05RawReportDecoder.TryDecode(counterClockwise, out snapshot, out error),
        error ?? "decode failed");
    Assert(
        snapshot.Roller == RollerPulse.CounterClockwise,
        "0xFF should decode as counter-clockwise for kernel-normalized captures");
}

static void MalformedReportRejection()
{
    Assert(
        !Ack05RawReportDecoder.TryDecode(
            new byte[] { 0x02, 0xF0, 0x00 },
            out _,
            out _),
        "short report should be rejected");
    Assert(
        !Ack05RawReportDecoder.TryDecode(
            new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0 },
            out _,
            out _),
        "default keyboard report should be rejected");
}

static void AllKeyBitMapping()
{
    var buttons = Enum.GetValues<Ack05Button>();
    for (var index = 0; index < buttons.Length; index++)
    {
        var report = new byte[] { 0x02, 0xF0, 0, 0, 0, 0, 0, 0 };
        if (index < 8)
        {
            report[2] = (byte)(1 << index);
        }
        else
        {
            report[3] = (byte)(1 << (index - 8));
        }

        Assert(
            Ack05RawReportDecoder.TryDecode(report, out var snapshot, out var error),
            error ?? $"{buttons[index]} decode failed");

        for (var candidate = 0; candidate < buttons.Length; candidate++)
        {
            Assert(
                snapshot.IsHeld(buttons[candidate]) == (candidate == index),
                $"{buttons[index]} report mapped incorrectly to {buttons[candidate]}");
        }
    }
}

static void PhysicalTransitionTracking()
{
    var tracker = new Ack05TransitionTracker();
    var down = tracker.Apply(new Ack05Snapshot(0x0403, RollerPulse.Clockwise));

    Assert(down.Count == 4, "expected K1 down, K2 down, roller press, and roller pulse");
    Assert(down[0] == new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down), "K1 down missing");
    Assert(down[1] == new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down), "K2 down missing");
    Assert(
        down[2] == new PhysicalInputEvent(Ack05Control.RollerPress, PhysicalEventType.Down),
        "roller press down missing");
    Assert(
        down[3] == new PhysicalInputEvent(Ack05Control.RollerClockwise, PhysicalEventType.Pulse),
        "roller pulse missing");

    var unchanged = tracker.Apply(new Ack05Snapshot(0x0403, RollerPulse.None));
    Assert(unchanged.Count == 0, "unchanged held state should emit no events");

    var up = tracker.Apply(new Ack05Snapshot(0, RollerPulse.None));
    Assert(up.Count == 3, "expected three key-up events");
    Assert(up.All(item => item.Type == PhysicalEventType.Up), "reset should emit only up events");
}

static void MomentaryLayerRouting()
{
    var router = new Ack05LayerRouter();

    var layerDown = router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    Assert(layerDown.Single().Kind == LayerRouteKind.LayerChanged, "K1 down should change layer");
    Assert(router.ActiveLayer == 1, "active layer should be 1");

    var keyDown = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    Assert(keyDown.Single().Layer == 1, "K2 down should route through layer 1");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 0, "K1 up should restore base layer");

    var keyUp = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(
        keyUp.Single().Layer == 1,
        "K2 up must use the layer captured on down even after modifier release");

    var roller = router.Route(
        new PhysicalInputEvent(Ack05Control.RollerClockwise, PhysicalEventType.Pulse));
    Assert(roller.Single().Layer == 0, "roller pulse should use current layer");
}

static void ToggleLayerRouting()
{
    var router = new Ack05LayerRouter(Ack05LayerSwitchMode.Toggle);

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    Assert(router.ActiveLayer == 1, "first K1 down should latch Layer 1");
    Assert(
        router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up)).Count == 0,
        "K1 up should not change a toggled layer");
    Assert(router.ActiveLayer == 1, "Layer 1 should remain latched after K1 up");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    Assert(router.ActiveLayer == 0, "second K1 down should return to Layer 0");
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
}

static void ConfigurableLayerControlRouting()
{
    var router = new Ack05LayerRouter(modifier: Ack05Control.K3);

    router.Route(new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down));
    Assert(router.ActiveLayer == 1, "configured K3 down should enter Layer 1");

    var keyDown = router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    router.Route(new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Up));
    var keyUp = router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));

    Assert(
        keyDown.Single().Layer == 1 && keyUp.Single().Layer == 1,
        "K1 should be bindable and preserve its captured layer when K3 is the layer control");
}

static void TapToggleHoldLayerRouting()
{
    var now = DateTimeOffset.Parse("2026-07-16T00:00:00Z");
    var router = new Ack05LayerRouter(
        Ack05LayerSwitchMode.TapToggleHold,
        TimeSpan.FromMilliseconds(200),
        now: () => now);

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(50);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 1, "short K1 tap should latch Layer 1");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(50);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 0, "second short K1 tap should unlatch Layer 1");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(250);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 0, "long K1 hold should return to Layer 0");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    var keyDown = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(50);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 0, "K1 chord should count as hold even below threshold");
    var keyUp = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(
        keyDown.Single().Layer == 1 && keyUp.Single().Layer == 1,
        "chord key-up should use its captured Layer 1");
}

static void KeymapLayerRouting()
{
    var profile = LayerTestProfile();
    var router = new Ack05KeymapLayerRouter(profile);

    var baseDown = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    var baseUp = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(baseDown.Single().Layer == 0 && baseUp.Single().Layer == 0,
        "K2 should use its Layer 0 A binding without K1");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    Assert(router.ActiveLayer == 1, "K1 MO(1) should activate Layer 1 while held");
    var shiftedDown = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    var shiftedUp = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(shiftedDown.Single().Layer == 1 && shiftedUp.Single().Layer == 1,
        "K1 + K2 should use Layer 1 B and preserve its layer until key-up");
    Assert(router.ActiveLayer == 0, "releasing K1 should restore Layer 0");
}

static void TransparentAndLayerPrecedence()
{
    var router = new Ack05KeymapLayerRouter(LayerTestProfile());
    router.Route(new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down));
    router.Route(new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 2, "TG(2) should latch Layer 2");

    var transparent = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(transparent.Single().Layer == 0,
        "transparent K2 on Layer 2 should fall through to Layer 0");

    router.Route(new PhysicalInputEvent(Ack05Control.K4, PhysicalEventType.Down));
    Assert(router.ActiveLayer == 3, "MO(3) should win over toggled Layer 2");
    var highest = router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    Assert(highest.Single().Layer == 3, "highest active non-transparent layer should win");
    var reset = router.Reset();
    Assert(router.ActiveLayer == 0, "reset should clear all active layers");
    Assert(reset.Any(item => item.Control == Ack05Control.K2 && item.InputType == PhysicalEventType.Up),
        "reset should release a pressed output action through its captured layer");
}

static void KeymapTapToggleHoldLayer()
{
    var now = DateTimeOffset.Parse("2026-07-16T00:00:00Z");
    var profile = new Ack05Profile
    {
        Id = "tap-hold",
        Name = "Tap Hold",
        Layers =
        [
            new Ack05Layer
            {
                Index = 0,
                Bindings =
                [
                    new Ack05Binding
                    {
                        Control = Ack05Control.K1,
                        Action = Ack05Action.TapToggleHoldLayer(1, 200),
                    },
                ],
            },
            new Ack05Layer
            {
                Index = 1,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K1, Action = Ack05Action.Transparent() },
                ],
            },
        ],
    };
    var router = new Ack05KeymapLayerRouter(profile, () => now);

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(50);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 1, "short solo tap should toggle Layer 1 on");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(250);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 1, "long hold should restore the previous toggled state");

    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(50);
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 1, "a chord should behave as hold even below threshold");
    router.Route(new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
}

static void DisconnectReset()
{
    var router = new Ack05LayerRouter();
    router.Route(new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    router.Route(new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down));

    var reset = router.Reset();
    Assert(
        reset.Any(item =>
            item.Kind == LayerRouteKind.Input &&
            item.Control == Ack05Control.K3 &&
            item.InputType == PhysicalEventType.Up &&
            item.Layer == 1),
        "reset should release K3 through its captured layer");
    Assert(
        reset.Any(item => item.Kind == LayerRouteKind.LayerChanged && item.Layer == 0),
        "reset should restore base layer");
}

static void InputSessionReset()
{
    var session = new Ack05InputSession();
    var down = session.ProcessSnapshot(new Ack05Snapshot(0x0003, RollerPulse.None));
    Assert(
        down.Any(item => item.Kind == LayerRouteKind.LayerChanged && item.Layer == 1),
        "session should enter Layer 1");
    Assert(
        down.Any(item =>
            item.Control == Ack05Control.K2 &&
            item.InputType == PhysicalEventType.Down &&
            item.Layer == 1),
        "session should route K2 down through Layer 1");

    var reset = session.Reset();
    Assert(
        reset.Any(item =>
            item.Control == Ack05Control.K2 &&
            item.InputType == PhysicalEventType.Up &&
            item.Layer == 1),
        "session reset should release K2 through its captured layer");
    Assert(
        reset.Any(item => item.Kind == LayerRouteKind.LayerChanged && item.Layer == 0),
        "session reset should restore Layer 0");
    Assert(session.ActiveLayer == 0, "session reset should leave Layer 0 active");
    Assert(session.Reset().Count == 0, "second session reset should be idempotent");
}

static void ReconnectServiceReset()
{
    using var cancellation = new CancellationTokenSource();
    var firstSource = new ScriptedReportSource(
        new byte[] { 0x02, 0xF0, 0x01, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x03, 0, 0, 0, 0, 0 },
        new IOException("simulated disconnect"));
    var secondSource = new ScriptedReportSource(
        new byte[] { 0x02, 0xF0, 0x04, 0, 0, 0, 0, 0 });
    var factory = new ScriptedReportSourceFactory(firstSource, secondSource);
    var sink = new RecordingInputSink(cancellation);
    var service = new Ack05InputService(
        factory,
        sink,
        new Ack05InputServiceOptions(TimeSpan.Zero));

    service.RunAsync(cancellation.Token).GetAwaiter().GetResult();

    Assert(factory.OpenCount == 2, "service should open a second connection after disconnect");
    Assert(firstSource.IsDisposed, "first connection should be disposed");
    Assert(secondSource.IsDisposed, "second connection should be disposed on shutdown");
    Assert(
        sink.Statuses.Count(status => status.State == Ack05ConnectionState.Connected) == 2,
        "service should publish two connected states");
    Assert(
        sink.Statuses.Any(status =>
            status.State == Ack05ConnectionState.Disconnected &&
            status.Message == "simulated disconnect"),
        "service should publish the disconnect reason");
    Assert(
        sink.Statuses.Last().State == Ack05ConnectionState.Stopped,
        "service should publish stopped last");
    Assert(
        sink.Events.Any(item =>
            item.Input.Control == Ack05Control.K2 &&
            item.Input.InputType == PhysicalEventType.Up &&
            item.Input.Layer == 1 &&
            !item.TokenCanBeCanceled),
        "disconnect reset should release Layer 1 K2 with a non-cancelable token");
    Assert(
        sink.Events.Any(item =>
            item.Input.Control == Ack05Control.K3 &&
            item.Input.InputType == PhysicalEventType.Up &&
            item.Input.Layer == 0 &&
            !item.TokenCanBeCanceled),
        "shutdown reset should release K3 with a non-cancelable token");
}

static void DefaultConfigurationRoundTrip()
{
    var original = Ack05Configuration.CreateDefault();
    var json = Ack05ConfigurationJson.Serialize(original);
    var restored = Ack05ConfigurationJson.Deserialize(json);

    Assert(restored.SchemaVersion == 3, "schemaVersion should round trip");
    Assert(restored.DefaultProfileId == "default", "default profile should round trip");
    Assert(
        restored.LayerSwitch is null,
        "schema v3 should not reserve a global layer control");
    Assert(restored.Profiles.Length == 1, "default configuration should have one profile");
    Assert(restored.Profiles[0].Layers.Length == 4, "default profile should have four layers");
    Assert(
        restored.Profiles[0].Layers.All(layer =>
            layer.Bindings.Length == 13),
        "default layers should contain all thirteen assignable controls");
    var k1 = restored.Profiles[0].Layers[0].Bindings.Single(binding => binding.Control == Ack05Control.K1);
    Assert(k1.Action.Kind == Ack05ActionKind.MomentaryLayer && k1.Action.TargetLayer == 1,
        "default K1 should use MO(1)");
}

static void ActionConfigurationRoundTrip()
{
    var configuration = new Ack05Configuration
    {
        Profiles =
        [
            new Ack05Profile
            {
                Id = "after-effects",
                Name = "After Effects",
                ApplicationMatch = new Ack05ApplicationMatch { ProcessName = "AfterFX.exe" },
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding
                            {
                                Control = Ack05Control.K2,
                                Action = Ack05Action.KeyboardChord(
                                    Ack05KeyboardKey.LeftControl,
                                    Ack05KeyboardKey.LeftShift,
                                    Ack05KeyboardKey.Z),
                            },
                            new Ack05Binding
                            {
                                Control = Ack05Control.K3,
                                Action = Ack05Action.TapToggleHoldLayer(1, 275),
                            },
                        ],
                    },
                    new Ack05Layer
                    {
                        Index = 1,
                        Bindings =
                        [
                            new Ack05Binding
                            {
                                Control = Ack05Control.RollerClockwise,
                                Action = Ack05Action.MouseWheel(2),
                            },
                        ],
                    },
                ],
            },
        ],
        DefaultProfileId = "after-effects",
    };

    var json = Ack05ConfigurationJson.Serialize(configuration);
    var restored = Ack05ConfigurationJson.Deserialize(json);
    var chord = restored.Profiles[0].Layers[0].Bindings[0].Action;
    var layerAction = restored.Profiles[0].Layers[0].Bindings[1].Action;
    var wheel = restored.Profiles[0].Layers[1].Bindings[0].Action;

    Assert(
        chord.Kind == Ack05ActionKind.KeyboardChord &&
        chord.Keys!.SequenceEqual(
        [
            Ack05KeyboardKey.LeftControl,
            Ack05KeyboardKey.LeftShift,
            Ack05KeyboardKey.Z,
        ]),
        "keyboard chord and key order should round trip");
    Assert(
        wheel.Kind == Ack05ActionKind.MouseWheel && wheel.WheelSteps == 2,
        "mouse wheel action should round trip");
    Assert(
        layerAction.Kind == Ack05ActionKind.TapToggleHoldLayer &&
        layerAction.TargetLayer == 1 &&
        layerAction.TapHoldThresholdMs == 275,
        "per-key layer target and threshold should round trip");
}

static void UnknownConfigurationFieldsRoundTrip()
{
    const string json = """
        {
          "schemaVersion": 1,
          "defaultProfileId": "default",
          "futureRoot": { "enabled": true },
          "profiles": [
            {
              "id": "default",
              "name": "Default",
              "futureProfile": 42,
              "layers": [
                {
                  "index": 0,
                  "bindings": [
                    {
                      "control": "k2",
                      "action": {
                        "kind": "disabled",
                        "futureAction": "kept"
                      }
                    }
                  ]
                },
                { "index": 1, "bindings": [] }
              ]
            }
          ]
        }
        """;

    var restored = Ack05ConfigurationJson.Deserialize(json);
    var serialized = Ack05ConfigurationJson.Serialize(restored);
    using var document = JsonDocument.Parse(serialized);
    var root = document.RootElement;
    var profile = root.GetProperty("profiles")[0];
    var action = profile.GetProperty("layers")[0]
        .GetProperty("bindings")
        .EnumerateArray()
        .Single(binding => binding.GetProperty("control").GetString() == "k2")
        .GetProperty("action");

    Assert(root.GetProperty("futureRoot").GetProperty("enabled").GetBoolean(),
        "unknown root field should survive round trip");
    Assert(
        restored.SchemaVersion == 3 &&
        restored.LayerSwitch is null &&
        restored.Profiles[0].Layers[0].Bindings.Single(binding => binding.Control == Ack05Control.K1)
            .Action.Kind == Ack05ActionKind.MomentaryLayer,
        "legacy configuration without layerSwitch should migrate to K1 MO(1)");
    Assert(profile.GetProperty("futureProfile").GetInt32() == 42,
        "unknown profile field should survive round trip");
    Assert(action.GetProperty("futureAction").GetString() == "kept",
        "unknown action field should survive round trip");
}

static void FutureConfigurationSchemaRejection()
{
    var json = Ack05ConfigurationJson.Serialize(Ack05Configuration.CreateDefault())
        .Replace("\"schemaVersion\": 3", "\"schemaVersion\": 4", StringComparison.Ordinal);

    AssertThrows<Ack05ConfigurationException>(
        () => Ack05ConfigurationJson.Deserialize(json),
        "future schemaVersion should be rejected");
}

static void InvalidConfigurationRejection()
{
    var configuration = new Ack05Configuration
    {
        Profiles =
        [
            new Ack05Profile
            {
                Id = "default",
                Name = "Default",
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding
                            {
                                Control = Ack05Control.K1,
                                Action = Ack05Action.Disabled(),
                            },
                            new Ack05Binding
                            {
                                Control = Ack05Control.K2,
                                Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.A),
                            },
                            new Ack05Binding
                            {
                                Control = Ack05Control.K2,
                                Action = Ack05Action.MouseWheel(1),
                            },
                        ],
                    },
                    new Ack05Layer { Index = 1 },
                ],
            },
        ],
    };

    var errors = Ack05ConfigurationValidator.Validate(configuration);
    Assert(!errors.Any(error => error.Contains("K1 is reserved", StringComparison.Ordinal)),
        "schema v3 should allow K1 bindings");
    Assert(errors.Any(error => error.Contains("duplicate binding", StringComparison.Ordinal)),
        "duplicate control binding should be rejected");
    AssertThrows<Ack05ConfigurationException>(
        () => Ack05ConfigurationJson.Serialize(configuration),
        "invalid configuration should not serialize");
}

static void ActionEngineChordLifecycle()
{
    var engine = new Ack05ActionEngine(CreateActionTestProfile());

    var firstDown = engine.Apply(Input(Ack05Control.K2, PhysicalEventType.Down));
    AssertSequence(
        firstDown,
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.LeftControl),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A));

    var secondDown = engine.Apply(Input(Ack05Control.K3, PhysicalEventType.Down));
    AssertSequence(secondDown, Ack05OutputEvent.KeyDown(Ack05KeyboardKey.B));

    var firstUp = engine.Apply(Input(Ack05Control.K2, PhysicalEventType.Up));
    AssertSequence(firstUp, Ack05OutputEvent.KeyUp(Ack05KeyboardKey.A));

    var secondUp = engine.Apply(Input(Ack05Control.K3, PhysicalEventType.Up));
    AssertSequence(
        secondUp,
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.B),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.LeftControl));
}

static void LayerActionValidation()
{
    var configuration = new Ack05Configuration
    {
        Profiles =
        [
            new Ack05Profile
            {
                Id = "default",
                Name = "Default",
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding
                            {
                                Control = Ack05Control.K1,
                                Action = Ack05Action.MomentaryLayer(9),
                            },
                            new Ack05Binding
                            {
                                Control = Ack05Control.RollerClockwise,
                                Action = Ack05Action.MomentaryLayer(1),
                            },
                        ],
                    },
                    new Ack05Layer { Index = 1 },
                ],
            },
        ],
    };

    var errors = Ack05ConfigurationValidator.Validate(configuration);
    Assert(
        errors.Any(error => error.Contains("targetLayer", StringComparison.Ordinal)),
        "out-of-range target layer should be rejected");
    Assert(
        errors.Any(error => error.Contains("pulse-only roller", StringComparison.Ordinal)),
        "momentary layer action on a pulse-only roller should be rejected");
}

static void ActionEnginePulseAndReset()
{
    var engine = new Ack05ActionEngine(CreateActionTestProfile());

    var pulse = engine.Apply(
        Input(Ack05Control.RollerClockwise, PhysicalEventType.Pulse));
    AssertSequence(
        pulse,
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.LeftShift),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.C),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.C),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.LeftShift));

    var wheel = engine.Apply(
        Input(Ack05Control.RollerCounterClockwise, PhysicalEventType.Pulse));
    AssertSequence(wheel, Ack05OutputEvent.MouseWheel(-2));

    engine.Apply(Input(Ack05Control.K2, PhysicalEventType.Down));
    var reset = engine.Reset();
    AssertSequence(
        reset,
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.A),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.LeftControl));
    Assert(engine.Reset().Count == 0, "action engine reset should be idempotent");
}

static void SendInputSafetyRegistry()
{
    var injector = new RecordingWindowsInputInjector();
    using var output = new SendInputAck05Output(injector);

    output.Apply(Ack05OutputEvent.KeyDown(Ack05KeyboardKey.LeftControl));
    output.Apply(Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A));
    output.Apply(Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A));
    output.Apply(Ack05OutputEvent.MouseWheel(1));

    Assert(output.PressedKeys.Count == 2, "safety registry should suppress duplicate key-down");
    output.ReleaseAll();
    Assert(output.PressedKeys.Count == 0, "panic release should empty the safety registry");
    AssertSequence(
        injector.Events,
        "down LeftControl",
        "down A",
        "wheel 1",
        "up A",
        "up LeftControl");
}

static void SendInputBestEffortPanicRelease()
{
    var injector = new RecordingWindowsInputInjector();
    using var output = new SendInputAck05Output(injector);
    output.Apply(Ack05OutputEvent.KeyDown(Ack05KeyboardKey.LeftControl));
    output.Apply(Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A));

    injector.FailKeyUp = Ack05KeyboardKey.A;
    AssertThrows<AggregateException>(
        output.ReleaseAll,
        "panic release should report a failed key-up");
    Assert(
        output.PressedKeys.SequenceEqual([Ack05KeyboardKey.A]),
        "failed key should remain registered while later releases continue");
    Assert(
        injector.Events.Contains("up LeftControl"),
        "panic release should continue after one failure");

    injector.FailKeyUp = null;
    output.ReleaseAll();
    Assert(output.PressedKeys.Count == 0, "retry should release the retained key");
}

static void ApplicationProfileSelection()
{
    var configuration = new Ack05Configuration
    {
        DefaultProfileId = "default",
        Profiles =
        [
            EmptyProfile("default", "Default"),
            EmptyProfile("after-effects", "After Effects", "AfterFX.exe"),
            EmptyProfile("chrome", "Chrome", "chrome"),
        ],
    };
    var selector = new Ack05ProfileSelector(configuration);

    Assert(selector.Select(null).Id == "default", "null process should use default profile");
    Assert(selector.Select("unknown.exe").Id == "default", "unknown process should use default profile");
    Assert(selector.Select("AfterFX").Id == "after-effects", "extensionless process should match");
    Assert(selector.Select("AFTERFX.EXE").Id == "after-effects", "process match should ignore case");
    Assert(selector.Select("chrome.exe").Id == "chrome", "optional .exe should normalize");

    var duplicate = new Ack05Configuration
    {
        DefaultProfileId = "default",
        Profiles =
        [
            EmptyProfile("default", "Default"),
            EmptyProfile("first", "First", "Example.exe"),
            EmptyProfile("second", "Second", "example"),
        ],
    };
    Assert(
        Ack05ConfigurationValidator.Validate(duplicate).Any(error =>
            error.Contains("duplicate applicationMatch", StringComparison.Ordinal)),
        "normalized duplicate process matches should be rejected");
}

static async Task LiveRuntimeLifecycle()
{
    var configuration = LiveConfiguration(Ack05KeyboardKey.A);
    var source = new ScriptedReportSource(
        new byte[] { 0x02, 0xF0, 0x02, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 });
    var injector = new RecordingWindowsInputInjector();
    var statuses = new List<Ack05LiveStatus>();
    await using var runtime = new Ack05LiveRuntime(
        new ScriptedReportSourceFactory(source),
        injector,
        new FixedForegroundProcessProvider());
    runtime.StatusChanged += statuses.Add;

    await runtime.StartAsync(configuration);
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
    while (injector.Events.Count < 2 && DateTime.UtcNow < deadline)
    {
        await Task.Delay(10);
    }

    AssertSequence(injector.Events, "down A", "up A");
    Assert(
        statuses.Any(status => status.State == Ack05LiveState.Connected),
        "live runtime should publish Connected");

    await runtime.StopAsync();
    Assert(!runtime.IsActive, "live runtime should be inactive after StopAsync");
    Assert(source.IsDisposed, "live runtime stop should dispose the report source");
}

static async Task LiveRuntimeConfigurationUpdate()
{
    var source = new ChannelReportSource();
    var injector = new RecordingWindowsInputInjector();
    await using var runtime = new Ack05LiveRuntime(
        new ScriptedReportSourceFactory(source),
        injector,
        new FixedForegroundProcessProvider());

    await runtime.StartAsync(LiveConfiguration(Ack05KeyboardKey.A));
    source.Write(new byte[] { 0x02, 0xF0, 0x02, 0, 0, 0, 0, 0 });
    await WaitForEventsAsync(injector, 1);

    runtime.UpdateConfiguration(LiveConfiguration(Ack05KeyboardKey.B));
    source.Write(new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 });
    source.Write(new byte[] { 0x02, 0xF0, 0x02, 0, 0, 0, 0, 0 });
    source.Write(new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 });
    await WaitForEventsAsync(injector, 4);

    AssertSequence(injector.Events, "down A", "up A", "down B", "up B");
    await runtime.StopAsync();
}

static async Task LiveRuntimeKeymapLayers()
{
    var source = new ScriptedReportSource(
        new byte[] { 0x02, 0xF0, 0x02, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x01, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x03, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x01, 0, 0, 0, 0, 0 },
        new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 });
    var injector = new RecordingWindowsInputInjector();
    await using var runtime = new Ack05LiveRuntime(
        new ScriptedReportSourceFactory(source),
        injector,
        new FixedForegroundProcessProvider());

    await runtime.StartAsync(KeymapLiveConfiguration());
    await WaitForEventsAsync(injector, 4);
    AssertSequence(injector.Events, "down A", "up A", "down B", "up B");
    await runtime.StopAsync();
}

static async Task LiveRuntimeTapHoldTimer()
{
    var source = new ChannelReportSource();
    var injector = new RecordingWindowsInputInjector();
    await using var runtime = new Ack05LiveRuntime(
        new ScriptedReportSourceFactory(source),
        injector,
        new FixedForegroundProcessProvider());

    await runtime.StartAsync(TapHoldConfiguration(
        Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
        Ack05Action.KeyboardChord(Ack05KeyboardKey.LeftShift)));
    source.Write(new byte[] { 0x02, 0xF0, 0x02, 0, 0, 0, 0, 0 });
    await WaitForEventsAsync(injector, 1);
    source.Write(new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 });
    await WaitForEventsAsync(injector, 2);

    AssertSequence(injector.Events, "down LeftShift", "up LeftShift");
    await runtime.StopAsync();
}

static async Task LiveRuntimeComboTimer()
{
    var source = new ChannelReportSource();
    var injector = new RecordingWindowsInputInjector();
    await using var runtime = new Ack05LiveRuntime(
        new ScriptedReportSourceFactory(source),
        injector,
        new FixedForegroundProcessProvider());

    await runtime.StartAsync(ComboConfiguration());
    source.Write(new byte[] { 0x02, 0xF0, 0x01, 0, 0, 0, 0, 0 });
    source.Write(new byte[] { 0x02, 0xF0, 0x03, 0, 0, 0, 0, 0 });
    await WaitForEventsAsync(injector, 1);
    source.Write(new byte[] { 0x02, 0xF0, 0x00, 0, 0, 0, 0, 0 });
    await WaitForEventsAsync(injector, 2);

    AssertSequence(injector.Events, "down Enter", "up Enter");
    await runtime.StopAsync();
}

static void TapHoldConfigurationRoundTrip()
{
    var configuration = TapHoldConfiguration(
        Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
        Ack05Action.KeyboardChord(Ack05KeyboardKey.LeftShift));
    var json = Ack05ConfigurationJson.Serialize(configuration);
    var restored = Ack05ConfigurationJson.Deserialize(json);
    var profile = restored.Profiles.Single();
    var definition = profile.TapHolds.Single();

    Assert(restored.SchemaVersion == 3, "Tap & Hold should serialize as schema v3");
    Assert(definition.Id == "th0" && definition.OnTap.Keys!.Single() == Ack05KeyboardKey.B,
        "Tap & Hold definition should survive round trip");
    Assert(profile.Layers[0].Bindings.Single(binding => binding.Control == Ack05Control.K2)
        .Action.TapHoldId == "th0", "Tap & Hold reference should survive round trip");

    var legacy = Ack05Configuration.CreateDefault();
    var legacyJson = Ack05ConfigurationJson.Serialize(legacy)
        .Replace("\"schemaVersion\": 3", "\"schemaVersion\": 2");
    var migrated = Ack05ConfigurationJson.Deserialize(legacyJson);
    Assert(migrated.SchemaVersion == 3 && migrated.Profiles.All(item => item.TapHolds.Length == 0),
        "schema v2 should load into schema v3 without changing legacy bindings");
}

static void TapHoldValidation()
{
    var configuration = TapHoldConfiguration(
        Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
        Ack05Action.KeyboardChord(Ack05KeyboardKey.LeftShift));
    var profile = configuration.Profiles.Single();
    var missingDefinition = new Ack05Configuration
    {
        Profiles =
        [
            new Ack05Profile
            {
                Id = profile.Id,
                Name = profile.Name,
                Layers = profile.Layers,
                TapHolds = [],
            },
        ],
    };
    Assert(Ack05ConfigurationValidator.Validate(missingDefinition)
        .Any(error => error.Contains("existing definition", StringComparison.Ordinal)),
        "missing Tap & Hold references must be rejected");

    var invalidHold = TapHoldConfiguration(
        Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
        Ack05Action.ToggleLayer(1));
    Assert(Ack05ConfigurationValidator.Validate(invalidHold)
        .Any(error => error.Contains("Hold slot", StringComparison.Ordinal)),
        "ToggleLayer must be rejected in a Hold slot");

    var rollerProfile = new Ack05Profile
    {
        Id = profile.Id,
        Name = profile.Name,
        TapHolds = profile.TapHolds,
        Layers =
        [
            new Ack05Layer
            {
                Index = 0,
                Bindings =
                [
                    new Ack05Binding
                    {
                        Control = Ack05Control.RollerClockwise,
                        Action = Ack05Action.TapHold("th0"),
                    },
                ],
            },
            new Ack05Layer { Index = 1 },
        ],
    };
    Assert(Ack05ConfigurationValidator.Validate(new Ack05Configuration { Profiles = [rollerProfile] })
        .Any(error => error.Contains("pulse-only", StringComparison.Ordinal)),
        "roller Tap & Hold must be rejected");
}

static void TapHoldKeyboardLifecycle()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var profile = TapHoldConfiguration(
        Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
        Ack05Action.KeyboardChord(Ack05KeyboardKey.LeftShift)).Profiles.Single();
    var router = new Ack05KeymapLayerRouter(profile, () => now);
    var engine = new Ack05ActionEngine(profile);

    AssertSequence(Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down)));
    now += TimeSpan.FromMilliseconds(100);
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.B),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.B));

    now += TimeSpan.FromSeconds(1);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(200);
    AssertSequence(
        ApplyRouted(router.Tick(), engine),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.LeftShift));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.LeftShift));
}

static void TapHoldLayerInterruptRouting()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var profile = TapHoldLayerProfile(Ack05TapHoldInterruptMode.HoldOnOtherKeyPress);
    var router = new Ack05KeymapLayerRouter(profile, () => now);
    var engine = new Ack05ActionEngine(profile);

    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down)),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.B));
    Assert(router.ActiveLayer == 1, "hold-on-press must activate the layer before routing the interrupting key");
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.B));
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 0, "releasing the Tap & Hold owner must deactivate MO");

    profile = TapHoldLayerProfile(Ack05TapHoldInterruptMode.HoldOnOtherKeyTap);
    router = new Ack05KeymapLayerRouter(profile, () => now);
    engine = new Ack05ActionEngine(profile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down)),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A));
    Assert(router.ActiveLayer == 0, "hold-on-tap must wait for the interrupting release");
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 1, "hold-on-tap must resolve after a complete other-key tap");

    profile = TapHoldLayerProfile(Ack05TapHoldInterruptMode.TimeoutOnly);
    router = new Ack05KeymapLayerRouter(profile, () => now);
    engine = new Ack05ActionEngine(profile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down));
    Assert(router.ActiveLayer == 0, "timeout-only must ignore other-key interrupts");
    now += TimeSpan.FromMilliseconds(200);
    ApplyRouted(router.Tick(), engine);
    Assert(router.ActiveLayer == 1, "timeout-only must resolve when its term expires");
}

static void TapHoldResetSafety()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var profile = TapHoldConfiguration(
        Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
        Ack05Action.KeyboardChord(Ack05KeyboardKey.LeftShift)).Profiles.Single();
    var router = new Ack05KeymapLayerRouter(profile, () => now);
    var engine = new Ack05ActionEngine(profile);

    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    AssertSequence(ApplyRouted(router.Reset(), engine));

    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(200);
    AssertSequence(ApplyRouted(router.Tick(), engine), Ack05OutputEvent.KeyDown(Ack05KeyboardKey.LeftShift));
    AssertSequence(ApplyRouted(router.Reset(), engine), Ack05OutputEvent.KeyUp(Ack05KeyboardKey.LeftShift));
    AssertSequence(engine.Reset());
}

static void ComboConfigurationAndValidation()
{
    var configuration = ComboConfiguration();
    var restored = Ack05ConfigurationJson.Deserialize(
        Ack05ConfigurationJson.Serialize(configuration));
    var combo = restored.Profiles.Single().Combos.Single();
    Assert(combo.Controls.SequenceEqual([Ack05Control.K1, Ack05Control.K2]),
        "Combo controls should survive round trip");
    Assert(combo.Action.Keys!.Single() == Ack05KeyboardKey.Enter,
        "Combo output should survive round trip");

    var profile = configuration.Profiles.Single();
    var invalid = CloneComboProfile(profile,
    [
        CloneCombo(profile.Combos.Single(), controls: [Ack05Control.K1, Ack05Control.K1]),
    ]);
    Assert(Ack05ConfigurationValidator.Validate(new Ack05Configuration { Profiles = [invalid] })
        .Any(error => error.Contains("exactly two distinct", StringComparison.Ordinal)),
        "duplicate Combo members must be rejected");

    invalid = CloneComboProfile(profile,
    [
        CloneCombo(profile.Combos.Single(), controls: [Ack05Control.K1, Ack05Control.RollerClockwise]),
    ]);
    Assert(Ack05ConfigurationValidator.Validate(new Ack05Configuration { Profiles = [invalid] })
        .Any(error => error.Contains("held controls", StringComparison.Ordinal)),
        "roller Combo members must be rejected");

    var rollerPressCombo = CloneComboProfile(profile,
    [
        CloneCombo(profile.Combos.Single(), controls: [Ack05Control.K1, Ack05Control.RollerPress]),
    ]);
    Assert(
        Ack05ConfigurationValidator.Validate(new Ack05Configuration { Profiles = [rollerPressCombo] }).Count == 0,
        "roller press should be accepted as a stateful Combo member");
}

static void ComboPressLifecycleAndFallback()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var profile = ComboConfiguration().Profiles.Single();
    var router = new Ack05KeymapLayerRouter(profile, () => now);
    var engine = new Ack05ActionEngine(profile);

    AssertSequence(Apply(router, engine,
        new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down)));
    now += TimeSpan.FromMilliseconds(30);
    AssertSequence(Apply(router, engine,
        new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down)));
    AssertSequence(ApplyRouted(router.Tick(), engine),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.Enter));
    AssertSequence(Apply(router, engine,
        new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.Enter));
    AssertSequence(Apply(router, engine,
        new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up)));

    now += TimeSpan.FromSeconds(1);
    router = new Ack05KeymapLayerRouter(profile, () => now);
    engine = new Ack05ActionEngine(profile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(50);
    AssertSequence(ApplyRouted(router.Tick(), engine),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A));
    AssertSequence(Apply(router, engine,
        new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.A));

    now += TimeSpan.FromSeconds(1);
    router = new Ack05KeymapLayerRouter(profile, () => now);
    engine = new Ack05ActionEngine(profile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K3, PhysicalEventType.Down)),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.A),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.B));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.A));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.B));
}

static void ComboTriggerModes()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var tapProfile = ComboConfiguration(
        Ack05ComboTrigger.TapOnly,
        holdTermMs: 180).Profiles.Single();
    var router = new Ack05KeymapLayerRouter(tapProfile, () => now);
    var engine = new Ack05ActionEngine(tapProfile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(20);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(40);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    AssertSequence(
        Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.Enter),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.Enter));

    now += TimeSpan.FromSeconds(1);
    var holdProfile = ComboConfiguration(
        Ack05ComboTrigger.HoldTogether,
        holdTermMs: 120).Profiles.Single();
    router = new Ack05KeymapLayerRouter(holdProfile, () => now);
    engine = new Ack05ActionEngine(holdProfile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(20);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(120);
    AssertSequence(ApplyRouted(router.Tick(), engine),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.Enter));
    AssertSequence(Apply(router, engine,
        new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up)),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.Enter));
}

static void ComboResetSafety()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var profile = ComboConfiguration().Profiles.Single();
    var router = new Ack05KeymapLayerRouter(profile, () => now);
    var engine = new Ack05ActionEngine(profile);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(10);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    AssertSequence(ApplyRouted(router.Tick(), engine),
        Ack05OutputEvent.KeyDown(Ack05KeyboardKey.Enter));
    AssertSequence(ApplyRouted(router.Reset(), engine),
        Ack05OutputEvent.KeyUp(Ack05KeyboardKey.Enter));
    AssertSequence(engine.Reset());
}

static void ComboMomentaryLayerReleasePolicy()
{
    var now = DateTimeOffset.Parse("2026-07-17T00:00:00Z");
    var original = ComboConfiguration().Profiles.Single();
    var source = original.Combos.Single();
    var combo = new Ack05ComboDefinition
    {
        Id = source.Id,
        Name = "K1 + K2 MO(1)",
        Controls = source.Controls,
        Layers = source.Layers,
        Release = Ack05ComboRelease.AllMembers,
        Action = Ack05Action.MomentaryLayer(1),
    };
    var profile = CloneComboProfile(original, [combo]);
    var router = new Ack05KeymapLayerRouter(profile, () => now);
    var engine = new Ack05ActionEngine(profile);

    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Down));
    now += TimeSpan.FromMilliseconds(10);
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Down));
    ApplyRouted(router.Tick(), engine);
    Assert(router.ActiveLayer == 1, "Combo MO should activate its target layer");
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K1, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 1, "AllMembers must retain MO until both members are released");
    Apply(router, engine, new PhysicalInputEvent(Ack05Control.K2, PhysicalEventType.Up));
    Assert(router.ActiveLayer == 0, "last member release must deactivate Combo MO");
}

static IReadOnlyList<Ack05OutputEvent> Apply(
    Ack05KeymapLayerRouter router,
    Ack05ActionEngine engine,
    PhysicalInputEvent input) => ApplyRouted(router.Route(input), engine);

static IReadOnlyList<Ack05OutputEvent> ApplyRouted(
    IEnumerable<LayerRoutedEvent> routedEvents,
    Ack05ActionEngine engine) => routedEvents.SelectMany(engine.Apply).ToArray();

static Ack05Configuration TapHoldConfiguration(Ack05Action tap, Ack05Action hold) =>
    new()
    {
        DefaultProfileId = "default",
        Profiles =
        [
            new Ack05Profile
            {
                Id = "default",
                Name = "Default",
                TapHolds =
                [
                    new Ack05TapHoldDefinition
                    {
                        Id = "th0",
                        Name = "B / Shift",
                        TappingTermMs = 200,
                        OnTap = tap,
                        OnHold = hold,
                    },
                ],
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding
                            {
                                Control = Ack05Control.K2,
                                Action = Ack05Action.TapHold("th0"),
                            },
                        ],
                    },
                    new Ack05Layer { Index = 1 },
                ],
            },
        ],
    };

static Ack05Configuration ComboConfiguration(
    Ack05ComboTrigger trigger = Ack05ComboTrigger.Press,
    int? holdTermMs = null) =>
    new()
    {
        DefaultProfileId = "default",
        Profiles =
        [
            new Ack05Profile
            {
                Id = "default",
                Name = "Default",
                ComboDefaults = new Ack05ComboDefaults { TermMs = 50 },
                Combos =
                [
                    new Ack05ComboDefinition
                    {
                        Id = "combo0",
                        Name = "K1 + K2 Enter",
                        Controls = [Ack05Control.K1, Ack05Control.K2],
                        Layers = [0],
                        Trigger = trigger,
                        HoldTermMs = trigger == Ack05ComboTrigger.Press
                            ? null
                            : holdTermMs ?? Ack05ComboDefinition.DefaultHoldTermMs,
                        Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.Enter),
                    },
                ],
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding { Control = Ack05Control.K1, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.A) },
                            new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.B) },
                        ],
                    },
                    new Ack05Layer { Index = 1 },
                ],
            },
        ],
    };

static Ack05Profile CloneComboProfile(
    Ack05Profile profile,
    Ack05ComboDefinition[] combos) =>
    new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Layers = profile.Layers,
        TapHolds = profile.TapHolds,
        ComboDefaults = profile.ComboDefaults,
        Combos = combos,
    };

static Ack05ComboDefinition CloneCombo(
    Ack05ComboDefinition combo,
    Ack05Control[] controls) =>
    new()
    {
        Id = combo.Id,
        Name = combo.Name,
        Controls = controls,
        Layers = combo.Layers,
        TermMs = combo.TermMs,
        Ordered = combo.Ordered,
        Trigger = combo.Trigger,
        HoldTermMs = combo.HoldTermMs,
        Release = combo.Release,
        Action = combo.Action,
    };

static Ack05Profile TapHoldLayerProfile(Ack05TapHoldInterruptMode interruptMode) =>
    new()
    {
        Id = "default",
        Name = "Default",
        TapHolds =
        [
            new Ack05TapHoldDefinition
            {
                Id = "th0",
                Name = "B / Layer 1",
                TappingTermMs = 200,
                InterruptMode = interruptMode,
                OnTap = Ack05Action.KeyboardChord(Ack05KeyboardKey.B),
                OnHold = Ack05Action.MomentaryLayer(1),
            },
        ],
        Layers =
        [
            new Ack05Layer
            {
                Index = 0,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.TapHold("th0") },
                    new Ack05Binding { Control = Ack05Control.K3, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.A) },
                ],
            },
            new Ack05Layer
            {
                Index = 1,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.Transparent() },
                    new Ack05Binding { Control = Ack05Control.K3, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.B) },
                ],
            },
        ],
    };

static Ack05Configuration LiveConfiguration(Ack05KeyboardKey key) =>
    new()
    {
        DefaultProfileId = "default",
        Profiles =
        [
            new Ack05Profile
            {
                Id = "default",
                Name = "Default",
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding
                            {
                                Control = Ack05Control.K2,
                                Action = Ack05Action.KeyboardChord(key),
                            },
                        ],
                    },
                    new Ack05Layer { Index = 1 },
                ],
            },
        ],
    };

static Ack05Configuration KeymapLiveConfiguration() =>
    new()
    {
        DefaultProfileId = "default",
        Profiles =
        [
            new Ack05Profile
            {
                Id = "default",
                Name = "Default",
                Layers =
                [
                    new Ack05Layer
                    {
                        Index = 0,
                        Bindings =
                        [
                            new Ack05Binding { Control = Ack05Control.K1, Action = Ack05Action.MomentaryLayer(1) },
                            new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.A) },
                        ],
                    },
                    new Ack05Layer
                    {
                        Index = 1,
                        Bindings =
                        [
                            new Ack05Binding { Control = Ack05Control.K1, Action = Ack05Action.Transparent() },
                            new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.B) },
                        ],
                    },
                ],
            },
        ],
    };

static async Task WaitForEventsAsync(RecordingWindowsInputInjector injector, int count)
{
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
    while (injector.Events.Count < count && DateTime.UtcNow < deadline)
    {
        await Task.Delay(10);
    }

    Assert(injector.Events.Count >= count, $"expected at least {count} output events");
}

static Ack05Profile EmptyProfile(string id, string name, string? processName = null) =>
    new()
    {
        Id = id,
        Name = name,
        ApplicationMatch = processName is null
            ? null
            : new Ack05ApplicationMatch { ProcessName = processName },
        Layers =
        [
            new Ack05Layer { Index = 0 },
            new Ack05Layer { Index = 1 },
        ],
    };

static Ack05Profile CreateActionTestProfile() =>
    new()
    {
        Id = "test",
        Name = "Test",
        Layers =
        [
            new Ack05Layer
            {
                Index = 0,
                Bindings =
                [
                    new Ack05Binding
                    {
                        Control = Ack05Control.K2,
                        Action = Ack05Action.KeyboardChord(
                            Ack05KeyboardKey.LeftControl,
                            Ack05KeyboardKey.A),
                    },
                    new Ack05Binding
                    {
                        Control = Ack05Control.K3,
                        Action = Ack05Action.KeyboardChord(
                            Ack05KeyboardKey.LeftControl,
                            Ack05KeyboardKey.B),
                    },
                    new Ack05Binding
                    {
                        Control = Ack05Control.RollerClockwise,
                        Action = Ack05Action.KeyboardChord(
                            Ack05KeyboardKey.LeftShift,
                            Ack05KeyboardKey.C),
                    },
                    new Ack05Binding
                    {
                        Control = Ack05Control.RollerCounterClockwise,
                        Action = Ack05Action.MouseWheel(-2),
                    },
                ],
            },
            new Ack05Layer { Index = 1 },
        ],
    };

static Ack05Profile LayerTestProfile() =>
    new()
    {
        Id = "layers",
        Name = "Layers",
        Layers =
        [
            new Ack05Layer
            {
                Index = 0,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K1, Action = Ack05Action.MomentaryLayer(1) },
                    new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.A) },
                    new Ack05Binding { Control = Ack05Control.K3, Action = Ack05Action.ToggleLayer(2) },
                    new Ack05Binding { Control = Ack05Control.K4, Action = Ack05Action.MomentaryLayer(3) },
                ],
            },
            new Ack05Layer
            {
                Index = 1,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K1, Action = Ack05Action.Transparent() },
                    new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.B) },
                ],
            },
            new Ack05Layer
            {
                Index = 2,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.Transparent() },
                ],
            },
            new Ack05Layer
            {
                Index = 3,
                Bindings =
                [
                    new Ack05Binding { Control = Ack05Control.K2, Action = Ack05Action.KeyboardChord(Ack05KeyboardKey.C) },
                ],
            },
        ],
    };

static LayerRoutedEvent Input(Ack05Control control, PhysicalEventType type, int layer = 0) =>
    new(LayerRouteKind.Input, control, type, layer);

static void AssertSequence<T>(IEnumerable<T> actual, params T[] expected)
{
    if (!actual.SequenceEqual(expected))
    {
        throw new InvalidOperationException(
            $"sequence mismatch: [{string.Join(", ", actual)}]");
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class ScriptedReportSourceFactory : IAck05ReportSourceFactory
{
    private readonly Queue<IAck05ReportSource> _sources;

    internal ScriptedReportSourceFactory(params IAck05ReportSource[] sources)
    {
        _sources = new Queue<IAck05ReportSource>(sources);
    }

    internal int OpenCount { get; private set; }

    public ValueTask<IAck05ReportSource?> TryOpenAsync(
        CancellationToken cancellationToken)
    {
        OpenCount++;
        return ValueTask.FromResult<IAck05ReportSource?>(
            _sources.Count > 0 ? _sources.Dequeue() : null);
    }
}

sealed class ScriptedReportSource : IAck05ReportSource
{
    private readonly Queue<object> _steps;

    internal ScriptedReportSource(params object[] steps)
    {
        _steps = new Queue<object>(steps);
    }

    public int InputReportLength => 32;

    internal bool IsDisposed { get; private set; }

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (_steps.Count == 0)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        var step = _steps.Dequeue();
        if (step is Exception exception)
        {
            throw exception;
        }

        var report = (byte[])step;
        report.CopyTo(buffer.Span);
        return report.Length;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

sealed class RecordingInputSink : IAck05InputSink
{
    private readonly CancellationTokenSource _cancellation;
    private int _connectedCount;

    internal RecordingInputSink(CancellationTokenSource cancellation)
    {
        _cancellation = cancellation;
    }

    internal List<Ack05ConnectionStatus> Statuses { get; } = new();

    internal List<(LayerRoutedEvent Input, bool TokenCanBeCanceled)> Events { get; } = new();

    public ValueTask OnConnectionStatusAsync(
        Ack05ConnectionStatus status,
        CancellationToken cancellationToken)
    {
        Statuses.Add(status);
        if (status.State == Ack05ConnectionState.Connected)
        {
            _connectedCount++;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnInputAsync(
        LayerRoutedEvent input,
        CancellationToken cancellationToken)
    {
        Events.Add((input, cancellationToken.CanBeCanceled));
        if (_connectedCount == 2 &&
            input.Control == Ack05Control.K3 &&
            input.InputType == PhysicalEventType.Down)
        {
            _cancellation.Cancel();
        }

        return ValueTask.CompletedTask;
    }
}

sealed class RecordingWindowsInputInjector : IAck05WindowsInputInjector
{
    internal List<string> Events { get; } = new();

    internal Ack05KeyboardKey? FailKeyUp { get; set; }

    public void KeyDown(Ack05KeyboardKey key) => Events.Add($"down {key}");

    public void KeyUp(Ack05KeyboardKey key)
    {
        Events.Add($"up {key}");
        if (FailKeyUp == key)
        {
            throw new IOException($"simulated {key} key-up failure");
        }
    }

    public void MouseWheel(int steps) => Events.Add($"wheel {steps}");
}

sealed class FixedForegroundProcessProvider : IForegroundProcessProvider
{
    public string? GetForegroundProcessName() => null;
}

sealed class ChannelReportSource : IAck05ReportSource
{
    private readonly Channel<byte[]> _reports = Channel.CreateUnbounded<byte[]>();

    public int InputReportLength => 32;

    internal void Write(byte[] report) => _reports.Writer.TryWrite(report);

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var report = await _reports.Reader.ReadAsync(cancellationToken);
        report.CopyTo(buffer.Span);
        return report.Length;
    }

    public ValueTask DisposeAsync()
    {
        _reports.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
