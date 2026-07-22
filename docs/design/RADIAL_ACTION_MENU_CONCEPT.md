# Radial Action Menu Concept

Status: Implemented in schema v4; visual direction approved and Release UI checked.

Date: 2026-07-21

## Product idea

The reusable interaction is configured from three logical inputs:

1. Radial menu activation.
2. Move selection forward.
3. Move selection backward.

Holding the activation input opens a lightweight on-screen radial menu. Pressing
the forward/backward inputs moves the highlight through 4-8 configured actions.
Releasing the activation input executes the highlighted action.

```text
Hold K8 -> radial menu appears
Roll CW / CCW -> forward / backward
Release K8 -> execute selected action
```

For ACK05, a typical assignment is K1 for activation and the roller directions for
forward/backward. Other devices may use any three compatible buttons or pulses.
The behavior must not depend on a roller being present.

The menu is a shared stateful action, not a new transport. It consumes normalized
physical input events and must not require firmware, pairing, driver, or HidHide
changes. ACK05 is the first target, while the contract remains reusable by future
device adapters.

## Interaction requirements

- Support 4, 6, or 8 segments. The editor creates eight safe Disabled segments by default.
- Open only after a configurable hold threshold so a normal Tap action can coexist.
- Start with no executable item selected. The first roller detent selects an item;
  releasing without selection cancels safely.
- Forward/backward moves one item per configurable pulse count. A roller detent is
  one possible pulse source, not part of the radial-menu contract.
- While open, consume navigation events so the foreground application does not also
  receive their ordinary binding.
- The activation and navigation inputs must be independently assignable and
  validated for conflicts.
- Releasing the trigger executes the current item once, then closes the overlay.
- Escape, Stop Live, disconnect, profile/config change, window shutdown, or losing
  the trigger key cancels without executing.
- The overlay must not steal keyboard focus from the foreground application.
- Position the overlay near the pointer by default, clamped to the active monitor;
  allow Center of active monitor as an alternative.
- Support mixed-DPI and multi-monitor placement.
- Show a continuous wheel with Windows system icons, names outside the wheel,
  strong selected contrast, and a deliberately small center.

## Action model

Each segment reuses the shared leaf-action chooser. Initial safe outputs are:

- keyboard chord;
- mouse wheel;
- layer action where lifecycle semantics are valid;
- the implemented shell-delegated Open Target action.

Arbitrary command lines, scripts, and shell strings are not accepted. Program
launch requires a separate allowlisted action model, explicit file selection, and
validation. Composite radial menus, nested Tap & Hold, and Combo outputs are out of
scope for the first slice.

## UI direction

Add a `Radial Menus` workspace beside Keymap, Tap & Hold, and Combos. A
definition editor contains Name, Segment Count, Hold Threshold, Position, Navigation
Step, activation input, forward input, backward input, and the ordered segment
actions. The Keymap may offer a shortcut that assigns the activation input to the
currently selected physical control.

The visual language should follow the current UI references: generous white/light-
gray space, thin black outlines, rounded containers, sparse labels, and a solid
black selected segment. The overlay itself should be visually quieter than the
foreground creative application.

## Implemented visual and placement contract

- The overlay has a transparent 520 x 520 bound, a 146-pixel outer wheel radius,
  a 40-pixel center radius, and eight-segment-safe external-label radius.
- Segment zero is centered at twelve o'clock and the editor list advances clockwise.
- The selected segment uses solid near-black; other segments use the app's warm
  off-white surface. Thin dividers and leaders retain the existing monochrome style.
- External labels receive a one-pixel white contrast halo so foreground-app text
  does not erase them. Leaders receive the same contrast treatment.
- Built-in `Segoe MDL2 Assets` glyphs cover the approved sample semantics and
  configured action kinds without adding a runtime dependency.
- Pointer placement subtracts half the overlay size, then clamps X/Y to the active
  monitor `WorkingArea` with a 16-pixel margin. The complete form remains visible;
  it is never clipped or scaled down at an edge.
- `WS_EX_NOACTIVATE`, tool-window, and transparent hit-test behavior keep the
  overlay focusless and click-through. A temporary bare Escape hotkey cancels it.

Release QA on 2026-07-21 used an isolated recovery path. The editor and overlay
were captured at 1406 x 933, and the window was moved partly off-screen to force a
left-edge placement. The overlay origin matched active-monitor working-area left
plus 16 pixels and all 520 pixels remained visible. Mixed-DPI interaction is not
yet hardware-verified.

## Main technical risks

- focus stealing or input reaching both the overlay and foreground application;
- a missing physical Up causing accidental execution;
- DPI/monitor coordinate mistakes;
- launching an unintended program;
- roller input arriving before the overlay is ready.

All cancellation paths therefore default to no execution, and the overlay state
must be owned by the same panic-release lifecycle as held keyboard output.
