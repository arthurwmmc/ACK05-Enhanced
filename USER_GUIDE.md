# ACK05 Enhanced user guide

## Editing model

The editor has two states:

- **Working draft**: every valid edit is applied to the open editor and, while
  Output is ON, hot-swapped into the runtime immediately.
- **Saved configuration**: JSON written only when you choose Save.

Undo and Redo remain available after Save. `Revert to Saved` restores the last
explicit Save and is itself undoable. Closing the tool clears session history.
Dirty drafts are also written separately under Local App Data after a short delay.
After an interrupted or unsaved session, startup offers Restore or Discard. Restore
keeps the explicit-Save baseline for Revert but does not restore Undo/Redo history
or turn Output ON.

## Assign a normal key

1. Open **Keymap** and select a profile and layer.
2. Select K1-K10 or a roller direction on the device map.
3. Choose an action once in the Action Palette.
4. Adjust only the parameters shown in the right inspector.
5. Choose Save when the draft should become the persistent baseline.

Keyboard actions can combine modifiers and a normal key. The Action Palette can
also be dragged onto K1-K10. Pulse-only roller directions reject actions that
require a later key-up, such as MO and Tap & Hold.

## Keyboard operation

Tab and Shift+Tab move through the workspaces, physical K1-K10 keys, inspector,
and enabled Action Palette cards. Owner-drawn controls use a dotted monochrome
focus cue. When the roller has focus, Left and Right Arrow select CCW and CW for
editing. Unavailable action cards are omitted from keyboard traversal.

## Layers

- `MO(n)` activates Layer n only while its owner key is held.
- `TG(n)` toggles Layer n on each key-down.
- `Transparent` falls through to the highest lower active layer with a binding.
- Tap Toggle / Hold Layer keeps the legacy short-tap toggle and held momentary
  behavior.

Layers are created before layer actions are assigned. Profiles contain 2-8
layers. Removing the last layer is blocked while another action references it.

## Tap & Hold

Open **Tap & Hold**, create a reusable definition, then configure:

- an independent Tap action;
- an independent Hold action;
- the tapping term;
- how another key interrupts the pending decision.

Return to Keymap and assign the definition to any K1-K10 key. Definitions that
are still referenced cannot be deleted. Double Tap and Tap + Hold are planned but
not implemented in the current slice.

## Combos

Open **Combos** and create a definition. Select exactly two K1-K10 members on the
map, choose the layer scope and timing policy, then assign its output from the
shared Action Palette.

Combo candidates are evaluated before their individual bindings. If the pair
does not match its timing/order/trigger conditions, buffered member events are
replayed through the normal Tap & Hold and keymap path. Three-key Combos are
deliberately unsupported.

Keymap shows current-layer Combo badges by default. Use the compact `Combos <count>`
button above the map to hide them; it changes to `Combos OFF`. This is only a view
preference for the current app session. Combo definitions and recognition remain
active, and Save/Undo are not affected.

## ACK05 output

The editor is always available, but ACK05 capture and synthetic output are a
separate safety boundary. The header starts at `Output OFF`; click it to enable
output only when ready. Before enabling it:

1. Keep a normal keyboard and mouse available.
2. Use a harmless test target such as an empty text editor.
3. Review the raw-mode and `SendInput` confirmation.

While active the same button reads `Output ON`. Click it immediately if a mapping
behaves unexpectedly. Turning output OFF, disconnect, configuration replacement,
and normal shutdown release synthetic held keys. Waiting for the device and
connection state are reported in the bottom status bar.

## Profiles and files

Profiles can match the foreground process name; an unmatched process uses the
default profile. Configuration is versioned JSON. Schema v1/v2 files load into
the current schema-v3 model without silently converting legacy Tap-Hold actions.

Personal `ack05-config.json` files at the repository root are ignored by Git.
Place deliberate shareable examples under `examples/` after removing private
process names or other local details.
