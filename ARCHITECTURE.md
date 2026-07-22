# Architecture

## Data flow

```text
ACK05 USB / receiver
        |
Windows HID transport + documented raw-mode opt-in
        |
raw report decoder + transition tracker
        |
physical Down / Up / Pulse (no reserved key)
        |
profile-aware keymap layer router
  - active layer stack
  - Transparent fall-through
  - MO / TG / Tap-Toggle-Hold
  - down-time layer capture
  - reusable Tap & Hold resolver + interrupt modes
  - exactly-two-key Combo resolver before ordinary binding resolution
  - hold-to-open Radial Menu resolver after Combo detection
  - timer-driven Hold confirmation
        |
logical action engine
        |
Windows output boundary
  - SendInput + pressed-key safety registry
  - shell-delegated Open Target launcher
        |
GUI / Probe CLI
```

The input service can still instantiate the legacy two-layer router for probe
and regression tests. Configured GUI/CLI runs use pass-through physical events
and resolve schema v4 layers, Combo definitions, Tap & Hold definitions, and leaf
actions after the foreground profile is selected.

## Module responsibilities

### `Ack05Enhanced.Core`

Contains no Windows APIs. It owns report decoding, physical transition tracking,
reconnect-session reset events, schema v4 configuration and validation, v1-v3-to-v4
migration, profile selection, `Ack05KeymapLayerRouter`, and logical actions.

`Ack05KeymapLayerRouter` owns active momentary/toggled layers for one profile.
It resolves higher numbered active layers first, skips Transparent, captures the
resolved layer/action on key-down, and uses that capture on key-up. Reset emits
matching Up events for ordinary held controls and clears every active layer.
It also owns pending Tap & Hold state. Leaf action overrides are emitted only after
Tap or Hold is resolved; hosts call `Tick()` from a 10 ms timer for timeout-only holds.
Combo detection is the first stage inside the same router. It snapshots the active
layer, buffers candidate member events, suppresses a successful pair, and replays a
failed attempt into Tap & Hold and normal binding resolution using captured times.
The same `Tick()` confirms Press and Hold-together matches without a new HID event.
Radial Menu activation is then buffered before ordinary binding resolution. Its
hold threshold is confirmed by the same host timer; short activation presses are
replayed, while an open menu consumes its three controls and emits one selected
leaf action only when the activation input is released.

### `Ack05Enhanced.Windows`

Owns HID enumeration/opening, the documented ACK05 raw-mode command, reconnecting
report source, foreground process lookup, SendInput mapping, and
`Ack05LiveRuntime`. `ConfiguredAck05InputSink` selects the profile, then composes
the keymap layer router, action engine, and Windows output registry.

Configuration/profile/disconnect/shutdown changes run panic release before state
replacement. A held synthetic key therefore receives key-up even if layer or
profile semantics change while it is down.

`Ack05LiveRuntime` also publishes immutable radial overlay state to the App. The
state contains labels, action kinds, placement mode, and selected index only; the
Windows layer does not own the menu-selection state machine.

### `Ack05Enhanced.Probe`

Provides read-only enumeration/monitoring by default. Commands that send the
documented raw-mode command or Windows input require explicit flags. Configured
CLI run uses the same schema v4 router/action pipeline as the GUI. GUI target
launch is enabled only inside Output ON. CLI target launch is separately opt-in.

### `Ack05Enhanced.App`

Owns presentation and explicit persistence. It displays 2-8 layer tabs, the
hardware-shaped key map, action editor, target layer, and tap/hold threshold.
Adding a layer creates Transparent bindings. Removing the last layer is blocked
while any action targets it. The UI does not parse HID or call SendInput directly.

The Instrument Workbench shell keeps the physical map visible beside a
selected-key inspector and a shared action dock. Valid card clicks, parameter
changes, and physical-key drops replace the in-memory schema-v4 binding
immediately. Each committed replacement enters a session history before Live is
hot-swapped. Save updates the comparison baseline without clearing Undo/Redo;
Revert to Saved is itself undoable. A debounced application-layer recovery store
persists dirty drafts under Local App Data with their Save baseline, source path,
file fingerprint, and timestamp. Startup recovery never changes Core semantics;
clean state deletes the envelope and session history remains process-local.
Tap & Hold edits reusable dual-role definitions through a three-column list, map,
and inspector workspace. Its dedicated map presents the current layer, exact
assignment targets, and the keys referencing the selected definition. Combos edits
two-key definitions, layer scope, timing/release policy, and output through the
same action dock and a dedicated physical member map. The
Radial Menus workspace edits the three physical inputs, threshold, navigation
step, placement mode, segment count, external labels, and safe leaf outputs. Its
preview uses the same focusless click-through overlay as runtime but does not
route or execute an action. The overlay is positioned from the pointer or active
monitor center and clamps its full bounds to the selected monitor work area.
The
Keymap device control derives current-layer Combo badges directly from the active
profile; badges are presentation-only selectors and never participate in routing.
The Keymap's process-local visibility toggle only controls whether these selector
controls are created; it never changes Combo definitions or resolver eligibility.
Activating one navigates to the existing definition editor. The header's
`Output OFF` / `Output ON` control remains an explicit facade over
`Ack05LiveRuntime`; editor lifetime and output lifetime stay independent. A
horizontal `SplitContainer` keeps the editor above the scrollable action library;
its upper panel remains stable as the window grows, while the library receives
the extra height and also accepts direct vertical splitter adjustment.
Map and palette controls retain their child controls across ordinary selection
changes. Text, selection, capability, and motion state are invalidated only when
their value actually changes; category/search changes remain the boundary that
rebuilds Action Palette cards.

### `Ack05Enhanced.SelfTest`

Runs deterministic tests without hardware or a separate test framework. Coverage
includes legacy routing, schema migration, multilayer precedence, Transparent,
MO/TG behavior, Tap & Hold, Combo match/fallback/trigger/reset behavior, captured-
layer key-up, Radial Menu validation/lifecycle/reset behavior, live timer
confirmation, reconfiguration, and panic release.

## Configuration boundary

- Schema v4 accepts 2-8 contiguous layers per profile and has no global reserved
  layer key.
- Schema v1-v3 load through `Ack05ConfigurationMigrator`; serialization always
  validates and writes v4.
- Unknown extension data is preserved where the model retains the corresponding
  object.
- File location and atomic `.bak` persistence remain application concerns.

## Device boundary

Transport identifies `VID 0x28BD / PID 0x0202 / usage page 0xFF0A / usage
0x0001`. Raw-mode writes are restricted to the documented command and remain an
explicit opt-in. Firmware, pairing, drivers, and HidHide are outside the process.

The raw held-state mask contains K1-K10 plus the roller center switch. Core maps
the center switch to `RollerPress` with Down/Up transitions; clockwise and
counter-clockwise values remain stateless pulse controls. Configuration migration
adds a disabled `RollerPress` binding to older profiles without changing schema.

Direct Bluetooth currently exposes standard collections but has not provided the
device-identified physical key-state stream needed by the layer router. The
transport interface remains replaceable without changing layer semantics.

## Output boundary

Core emits logical keyboard, wheel, and Open Target events only. The action engine
reference-counts shared modifiers. The Windows adapter independently records injected pressed keys
and releases them in reverse order on normal Up, reset, failure, or dispose. This
two-level ownership is intentional safety redundancy.

Core also emits an OS-independent Open Target event containing one validated
target string. The Windows adapter expands environment variables and invokes the
shell `open` verb. Failures become non-fatal runtime notifications and do not
alter connection state or the pressed-key registry. Reset never repeats or closes
an already launched target.
