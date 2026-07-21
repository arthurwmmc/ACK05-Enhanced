# Undo and Safety

## Scope

ACK05 Enhanced sends operating-system input, so conventional document Undo cannot
reliably reverse an action. Safety is defined as never leaving synthetic keys or
buttons held after the physical input session ends.

## Input reset invariant

Every connection owns a fresh `Ack05InputSession`. The session tracks physical
held bits and the layer captured when each control went Down.

The service must call `Ack05InputSession.Reset()` before disposing a disconnected
source or completing process shutdown. Reset must:

1. derive Up events for every physically held key;
2. route each Up through the layer captured on Down;
3. restore Layer 0;
4. be idempotent when called again.

Reset events use `CancellationToken.None`. A canceled process token must not remove
the output sink's final chance to release held input.

## Transport safety

- Enumeration, `monitor`, `watch`, and Raw Input diagnostics are read-only by
  default.
- Only explicit `--enable-bitmask` may send the documented ACK05 command.
- Reconnecting raw-mode watch sends that command once per successful vendor
  collection connection.
- No command is sent to standard keyboard, mouse, digitizer, direct Bluetooth, or
  unidentified collections.
- Driver installation, driver stopping, HidHide configuration, firmware changes,
  and pairing changes are outside automatic application behavior.

## Output adapter implementation

The SendInput adapter maintains its own registry of successfully sent synthetic
key downs. It provides:

- release of a matching action when the routed Up arrives;
- release-all on disconnect, profile change, panic command, unhandled exception,
  and process shutdown;
- idempotent release-all;
- no automatic privilege elevation when UIPI blocks output;
- best-effort continuation when one release call fails, followed by an error log.

The adapter registry is a second safety boundary. It must not assume the Core
session reset alone proves that Windows accepted every release.

The Core action engine separately reference-counts shared chord keys. The Windows
adapter suppresses duplicate Down and unknown Up events as a second safety boundary.
`run` requires both `--enable-bitmask` and `--allow-sendinput`; neither device write
nor synthetic output is enabled by the read-only commands.

## Hardware verification

Real Windows SendInput was verified with F24 and Shift+F24 chords plus signed wheel
events. USB disconnect while F24 was held emitted F24 Up before reconnect. Ctrl+C
while Shift+F24 was held emitted F24 Up then Shift Up before Stopped. The adapter
reported panic release complete in both safety paths.

No startup execution or GUI panic button exists yet.

Working-draft recovery stores configuration JSON only. It never restores a Live
session, pressed-key registry, active layer, pending Tap & Hold/Combo timer, or any
synthetic output state. Restoring a draft therefore always starts with Live off and
the normal Layer 0 runtime baseline.

## Recovery

While `run` is active, keep a separate physical keyboard/mouse available. Ctrl+C,
ACK05 disconnect, and normal disposal all invoke panic release. Startup execution
must remain disabled until a GUI-visible panic control is implemented and tested.
