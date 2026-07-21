# Universal Input Remapper Vision

Status: Long-term product vision; concept only, not part of the current ACK05 implementation contract.

Date: 2026-07-17

## Goal

Grow ACK05 Enhanced from one-device software into an open, device-oriented input
remapping platform. The long-term product receives events from many physical input
devices, identifies their source where the operating system permits it, transforms
those events through user-editable keymaps, layers, Tap & Hold, Combos, and radial
menus, then emits safe logical actions.

The intended experience is more device-aware and visually editable than a general
automation script. It complements rather than initially replaces every AutoHotkey
use case.

## Product principles

- Treat each physical device and control as a first-class editable object.
- Keep device decoding separate from keymap/layer/action semantics.
- Normalize device-specific signals into a shared physical-input event model.
- Reuse one logical action model across ACK05, keyboards, mice, controllers, MIDI,
  and future adapters where feasible.
- Remain read-only toward devices by default. Device writes, driver changes, and
  system-wide hiding remain explicit opt-in operations.
- Guarantee matching release/cancellation for every held synthetic output.
- Prefer visual configuration, portable profiles, and inspectable versioned files.
- Do not promise universal interception when Windows or a vendor driver does not
  expose a source-identifiable event stream.

## Conceptual architecture

```text
Physical devices
  -> device adapters / transport discovery
  -> normalized physical input events
  -> device-scoped keymap and profile resolver
  -> layers / Tap & Hold / Combos / radial state
  -> logical actions
  -> Windows output adapters and future integrations
```

ACK05 is the first complete device adapter and reference implementation. Future
adapters must not require changes to the shared layer, binding, or action rules.

## Keymap requirements

The platform needs a native versioned keymap format containing at least:

- stable device identity and a human-readable device/layout name;
- physical control IDs and control capabilities (button, axis, wheel, pulse);
- profiles and application matching;
- layers, bindings, reusable definitions, and logical actions;
- optional visual layout metadata for the editor;
- compatibility version and migration rules.

Import is adapter-based rather than one universal parser. Candidate importers may
include the platform's own JSON first, followed by selected QMK/Vial/ZMK or other
formats after their semantics can be mapped without silently changing behavior.
Unsupported constructs must be reported, not guessed. Import and export are
separate capabilities; lossless round-trip is not assumed.

## Device support levels

1. Observe: identify and display source-attributable input.
2. Remap: consume or safely override controls and emit logical actions.
3. Enhanced: expose device-specific LEDs, displays, battery, or configuration via
   documented, explicit opt-in commands.

A device may remain at Observe when Windows merges its input, a vendor driver owns
the useful interface, or suppression cannot be made safe. “Any device” is a product
direction, not a claim that every device can reach every support level.

## Initial expansion path

1. Keep ACK05 as the stable reference adapter.
2. Generalize Core names and contracts only after a second real device proves the
   abstraction boundary.
3. Add a device registry and capability-described physical control model.
4. Define the native portable keymap package and explicit importer results.
5. Generalize the editor from a fixed ACK05 map to device-provided layouts.
6. Add adapters one at a time with hardware evidence and cancellation tests.

## Out of scope for the first generalization

- arbitrary kernel-level interception for every Windows input path;
- automatic driver replacement or HidHide configuration;
- silently importing unsupported keymap semantics;
- unrestricted scripts, shell commands, or network actions;
- claiming support for untested hardware from protocol similarity alone.

## Main risks

- Windows may merge keyboard/mouse input or prevent reliable source attribution.
- Remapping without suppressing the original signal can cause duplicate actions.
- Device IDs and paths may change across ports, pairing, or driver updates.
- Different keymap ecosystems attach different meanings to timing and layers.
- A generic abstraction designed only from ACK05 may encode ACK05 assumptions.

