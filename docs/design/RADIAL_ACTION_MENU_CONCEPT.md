# Radial Action Menu Concept

Status: Future concept; preserve in the roadmap, but do not include in the first
Tap & Hold / Combo schema-v3 implementation.

Date: 2026-07-17

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

- Support 4, 6, or 8 segments. Four is the simplest default.
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
- Show large icon/label pairs, strong selected contrast, and minimal secondary text.

## Action model

Each segment reuses the shared leaf-action chooser. Initial safe outputs are:

- keyboard chord;
- mouse wheel;
- layer action where lifecycle semantics are valid;
- a future allowlisted application/program action.

Arbitrary command lines, scripts, and shell strings are not accepted. Program
launch requires a separate allowlisted action model, explicit file selection, and
validation. Composite radial menus, nested Tap & Hold, and Combo outputs are out of
scope for the first slice.

## UI direction

Add a future `Radial Menus` workspace beside Keymap, Tap & Hold, and Combos. A
definition editor contains Name, Segment Count, Hold Threshold, Position, Navigation
Step, activation input, forward input, backward input, and the ordered segment
actions. The Keymap may offer a shortcut that assigns the activation input to the
currently selected physical control.

The visual language should follow the current UI references: generous white/light-
gray space, thin black outlines, rounded containers, sparse labels, and a solid
black selected segment. The overlay itself should be visually quieter than the
foreground creative application.

## Main technical risks

- focus stealing or input reaching both the overlay and foreground application;
- a missing physical Up causing accidental execution;
- DPI/monitor coordinate mistakes;
- launching an unintended program;
- roller input arriving before the overlay is ready.

All cancellation paths therefore default to no execution, and the overlay state
must be owned by the same panic-release lifecycle as held keyboard output.
