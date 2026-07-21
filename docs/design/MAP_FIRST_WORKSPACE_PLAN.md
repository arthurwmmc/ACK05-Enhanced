# Map-First Workspace Plan

Status: map-first Tap & Hold and output-clarity slices implemented

Date: 2026-07-21

## Purpose

The physical ACK05 map is the user's primary mental model. Editing a reusable
behavior must not hide where that behavior is assigned. Keymap, Tap & Hold, and
Combos should preserve spatial context while changing only the editor around it.

## Verified baseline

The user completed the 2026-07-20 hardware checklist successfully, including
momentary and toggle layers, non-K1 layer switching, Tap & Hold, two-key Combo,
Live hot-swap, and release safety. This UI work must not change those runtime or
configuration semantics.

## Observed problems

- Tap & Hold replaces the device map with a definition form.
- `Used by 1 key binding` gives a count but not its layer or physical key.
- `Assign to selected key` depends on selection remembered from another workspace.
- `TH(th0)` exposes an internal ID instead of the Tap and Hold result.
- Workspace changes and key selection sometimes feel heavier than the small amount
  of visible state change suggests.
- `Live` currently means both device capture and synthetic output preview. Its
  safety boundary is correct, but the label and state explanation are not yet clear.

## Target information architecture

```text
global profile / layer / workspace / Output OFF|ON / history / Save

definition list       persistent ACK05 map       contextual inspector
compact/collapsible   current layer and targets  parameters for this mode

resizable shared Action Palette
```

The map stays visible in Keymap, Tap & Hold, and Combos. The first implementation
may keep separate map-control instances, but they must share the same visual and
selection rules. A later refactor may host one shared instance without changing
the user-facing structure.

## Tap & Hold first slice

- Use the existing Combo three-column proportions as the starting point.
- Left: reusable definition list and exact assignment locations.
- Center: compact ACK05 map for the selected layer.
- Right: name, tapping term, interrupt mode, Tap/Hold slots, conditional parameters,
  and an assignment action naming its exact target.
- Selecting a physical K1-K10 key in this workspace changes the assignment target.
  Roller directions remain visible but cannot become Tap & Hold targets.
- Keys using the selected definition on the current layer receive a monochrome
  highlight. The selected assignment target remains independently visible.
- Reference copy uses locations such as `Used by L0 K7, L2 K4`, not only a count.
- A Tap & Hold binding on any map resolves to a compact result such as
  `Bksp/MO(1)` when its definition exists. The internal ID remains in tooltips and
  diagnostics, not as the primary key label.

## Performance investigation

The first pass removes known unnecessary UI work before adding instrumentation:

- do not destroy and recreate every visible Action Palette card when only selection
  state changed;
- do not invalidate or start motion on device keys whose text/selection is unchanged;
- avoid repeated capability refreshes when palette usage did not change;
- keep definition-list updates bracketed to avoid intermediate painting.

After this pass, measure workspace-switch and key-selection latency again. If the
delay remains perceptible, add development-only timing around configuration
serialization, Live validation/hot-swap, definition reload, and layout passes.

## Output clarity decision

The editor process and device output have separate lifetimes. Editing is always
available, but output remains an explicit opt-in because enabling it sends the
documented ACK05 raw-mode command and permits synthetic Windows input.

- Use a single stateful `Output OFF` / `Output ON` control; do not mix `Live` as a
  state with `Stop` as an action.
- Always launch and restore drafts with output OFF.
- Keep waiting, connected, and error details in the status bar instead of packing
  transport state into the button label.
- Valid edits still hot-swap while output is ON; Save remains file persistence.
- Turning output OFF, disconnect, and shutdown retain panic release.
- A future dedicated test/diagnostics surface may expose recent input timing, but
  it must not weaken this boundary.

## Compatibility and safety

- No configuration-schema change.
- No transport, firmware, driver, HidHide, or pairing change.
- No change to Tap & Hold, Combo, layer, Undo, Save, Revert, or release semantics.
- Existing configuration files remain compatible.
- Action Palette vertical resizing remains available.

## Acceptance

1. Open Tap & Hold and keep the ACK05 map visible.
2. Select a definition and identify every current-layer assignment on the map.
3. Read exact assignment locations without returning to Keymap.
4. Select K1-K10 in Tap & Hold and assign the definition to that named target.
5. See resolved Tap/Hold meaning on maps instead of only `TH(id)`.
6. Switch Keymap, Tap & Hold, and Combos repeatedly without visible card rebuilding,
   selection animation storms, or lost Action Palette height.
7. Pass all deterministic tests and repeat the user-verified hardware checklist
   without runtime behavior changes.
