# Undo and Safety

## Scope

ACK05 Enhanced sends operating-system input, so conventional document Undo cannot
reliably reverse an action. Safety is defined as never leaving synthetic keys or
buttons held after the physical input session ends.

## Input reset invariant

Every connection owns a fresh `Ack05InputSession`. The session tracks physical
held bits and the layer captured when each control went Down.

When a connection ends, the service calls `IAck05GestureSink.CancelCapture()`
before disposing the report source and publishing Disconnected or Stopped.
The GUI and configured Probe reset the gesture router under the same lock used
for input and timer ticks. This discards pending taps, combos and radial selections,
closes the overlay and releases already-held outputs through the action engine
and the Windows pressed-key registry. Cleanup is independent of the capture token.

The diagnostic watch sink still receives the physical Up events from
`Ack05InputSession.Reset()` with the layer captured on Down and a non-cancelable
token. Implement `IAck05GestureSink` for sinks that execute gestures on release.
Each reconnect starts a fresh physical session after cleanup completes.

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

Open Target is a one-shot shell action, not held input. GUI execution is gated by
Output ON and its first-use warning. Probe `run` additionally requires
`--allow-target-launch`; without it, target events are reported and ignored.
Target launch failure is non-fatal and never clears or corrupts the pressed-key
registry. Reset cannot undo a launched process, file, folder, or URL, so imported
configurations must be reviewed before enabling output.

An open Radial Menu has no output until the activation input is released with a
selection. Escape, Output OFF, disconnect, profile/config replacement, reset, and
shutdown cancel the menu before any segment action is emitted. Navigation inputs
are consumed while the menu is open. Preview is presentation-only and never emits
an action. If activation Up is missing, session reset closes the menu without
executing the highlighted segment.

## Hardware verification

Real Windows SendInput was verified with F24 and Shift+F24 chords plus signed wheel
events. USB disconnect while F24 was held emitted F24 Up before reconnect. Ctrl+C
while Shift+F24 was held emitted F24 Up then Shift Up before Stopped. The adapter
reported panic release complete in both safety paths.

No startup execution or GUI panic button exists yet.

Working-draft recovery stores configuration JSON only. It never restores a Live
session, pressed-key registry, active layer, pending Tap & Hold/Combo/Radial timer, or any
synthetic output state. Restoring a draft therefore always starts with Live off and
the normal Layer 0 runtime baseline.

## Recovery

While `run` is active, keep a separate physical keyboard/mouse available. Ctrl+C,
ACK05 disconnect, and normal disposal all invoke panic release. Startup execution
must remain disabled until a GUI-visible panic control is implemented and tested.
