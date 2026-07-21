# Bugfix Log

## 2026-07-20: K9 typography and Combo badge merged with selected keys

### Symptom

K9 displayed a larger key label and action label than the other physical keys.
A black Combo badge could visually merge into the black fill of a selected member
key when the compact map placed the badge over that key.

### Cause

Map typography was derived from control width even though K9 is intentionally
wide and K7 is intentionally tall. Combo badges used a black default fill while
selected physical keys use the same black accent.

### Fix

Derive both map label sizes from the shorter control dimension. Render Combo
badges with the keymap's light raised surface, black outline, and black text in
normal, hover, and focus states.

### Prevention

Non-square physical controls must use their shorter dimension for shared visual
scale. Overlay controls must retain a contrasting boundary against every state of
the surface underneath them.

### Verification

- Release build and deterministic self-tests.
- Compare K9 and K7 with square-key typography in the compact and full maps.
- Select a Combo member under its badge and confirm the badge remains distinct.

### Unverified

- 125/150/200% DPI remains a manual display check.

## 2026-07-20: Action Palette resize area could disappear

### Symptom

The Action Palette's short horizontal resize handle and vertically resizable area
could appear to be lost after a collapsed workspace or an unusually small initial
layout.

### Cause

The splitter minimums were only established during the first successful `Shown`
layout. A layout that was temporarily too short returned without retrying, and
the palette height was not explicitly preserved across panel collapse/restore.

### Fix

Retry splitter initialization when its size changes, retain the last visible
Action Palette height before collapse, restore that height on return, and keep the
160 px panel minimum once initialization succeeds.

### Prevention

Resizable shared panels must retain their last usable size across workspace
visibility changes. Deferred WinForms layouts must retry initialization rather
than treating an early undersized pass as final.

### Verification

- Release build and deterministic self-tests.
- Drag the divider, switch to Profiles, return to Keymap, and confirm the chosen
  Action Palette height and centered handle remain available.
- Start with a short layout, enlarge it, and confirm the panel minimum is applied.

### Unverified

- 125/150/200% DPI remains a manual hardware-display check.

## 2026-07-18: Workspace return clipped the inspector heading

### Symptom

After opening Combos and returning to Keymap, the right inspector could retain an
internal scroll offset. `KEY SETTINGS` disappeared and the selected-key caption
was partly clipped. Owner-drawn controls also accepted keyboard focus without a
consistent visible cue, and the roller directions had no keyboard selection path.

### Cause

Each workspace used an independent auto-scrolling inspector, but workspace
activation did not normalize its scroll origin after layout and data reload.
Owner-drawn buttons replaced native painting without restoring the focus cue.
The composite roller control was mouse-selectable only.

### Fix

Registered each workspace inspector and reset its scroll position after the
workspace reload completes. Added monochrome focus cues and accessible labels to
the shared owner-drawn controls. Made the roller a tab stop with Left/Right Arrow
selection, and shortened its inspector labels to `Roller CW` / `Roller CCW`.

### Prevention

Every workspace activation must leave its first field visible. Custom-painted
interactive controls must preserve focus visibility and an accessible name; a
mouse-only physical control needs an equivalent keyboard operation.

### Verification

- Reproduced the clipped Keymap inspector after creating `K1 + K2 -> Enter`.
- Repeated Combo to Keymap navigation after the fix; `KEY SETTINGS`, selected key,
  Combo badge, and action panel all remained fully visible.
- Tab and Shift+Tab visibly reached navigation buttons, K1-K10, and the roller.
- Left/Right Arrow selected the roller direction and updated the inspector.
- Release build completed with zero warnings/errors; self-tests passed 40/40.

### Unverified

- 125/150/200% DPI and runtime minimum-window resizing remain separate manual
  checks because non-client border manipulation was unavailable in that pass.

## 2026-07-17: Resizable Action Palette could abort startup

### Symptom

The first implementation of the resizable Action Palette exited before the main
window appeared with `InvalidOperationException` from `SplitterDistance`.

### Cause

The two panel minimum sizes were assigned while the `SplitContainer` still had its
small design-time size. WinForms immediately validated the existing splitter
distance against minimums that could not yet fit.

### Fix

Minimum sizes and the preferred 280 px palette height are now applied from the
form's `Shown` event, after the split container has its real client height. The
splitter distance is set first and the compatible minimums are applied afterward.

### Prevention

Do not assign mutually dependent WinForms layout constraints before the parent has
completed its first real layout. Keep the minimum-size arithmetic in one delayed
initializer and cover startup in the manual UI smoke test.

### Verification

- Release solution build completes with zero warnings and errors.
- The Release app opens at 1420 x 940 and shows the resizable divider.
- Dragging the divider exposes three complete Normal Keys rows and keeps the map visible.

### Unverified

- Multi-monitor DPI changes and 200% display scaling remain manual follow-up checks.

## 2026-07-17: Action preview could be mistaken for unrelated layer help

### Symptom

The selected row and device map showed K1 as `MO(1)`, but nearby static text about
Transparent layers made it appear that the action preview disagreed with the
selected binding. Exclusive actions also required unchecking before another action
could be chosen.

### Cause

The action preview displayed only an action value, without the selected physical
control or an indication that the palette represented a pending draft. The palette
also treated every alternative as blocked while an exclusive action was selected.

### Fix

The preview now uses `Draft for K1: MO(1)`-style text. Valid alternatives remain
enabled and replace the previous exclusive selection in one click. Only actions
that genuinely require held Down/Up events remain unavailable for roller pulses.

### Prevention

Keep selected control, draft value, and unsupported-action state derived from the
same editor selection. Gray states must represent runtime capability, not mutual
exclusivity.

### Verification

- Debug solution build completes without warnings or errors.
- Manual UI verification confirms K1 and its MO draft are named together.
- Manual palette verification confirms direct replacement between valid actions.

### Unverified

- Immediate draft assignment and Undo/Redo remain a later implementation phase;
  the interim UI still uses Apply Binding.

## 2026-07-16: First Save freezes the keymap editor

### Symptom

After applying a valid binding and clicking Save for the first time, the editor
became unresponsive. Windows displayed `ACK05 Enhanced Keymap Editor (Not
Responding)`, and no visible Save As window was available.

### Cause

The UI thread stalled while initializing the Explorer-style WinForms common
file dialog. The exact shell component responsible is machine-specific and
remains unverified, but the stall occurred before the dialog window was created
and before atomic file writing began.

### Fix

Open and Save As now use the built-in legacy Win32 dialog by setting
`AutoUpgradeEnabled = false`. Both dialogs start in the current configuration
directory, or Documents when no configuration path exists, and restore the
process directory after closing.

### Prevention

Keep the initial Save dialog in the manual GUI smoke test. Do not treat a modal
dialog as healthy unless a dialog window becomes visible and the editor returns
to a responsive state after cancellation.

### Verification

- Reproduced and captured the pre-fix `Not Responding` window.
- Built the Release solution with zero warnings and errors.
- Confirmed the first Save As dialog appears after the fix.
- Confirmed Cancel returns to a responsive editor.
- Saved a temporary JSON file and validated it with `validate-config`.
- Ran all 18 dependency-free self-tests successfully.

### Unverified

- The specific Windows shell extension or persisted Explorer state that caused
  the modern common dialog to stall.

## 2026-07-16: Redesigned device map crashes during startup

### Symptom

The first hardware-faithful layout build exited before showing its window.

### Cause

Adding the docked device surface triggered WinForms layout immediately, before
the K1-K10 button dictionary had finished initializing. `LayoutDevice` indexed
K1 during that partial construction state and threw `KeyNotFoundException`.

### Fix

`LayoutDevice` now returns until all ten key controls have been registered.

### Prevention

Treat constructor-time `Layout` and `Resize` callbacks as valid partial states.
Owner-drawn composite controls must guard child initialization before indexing.

### Verification

- Confirmed the original exception in the .NET Runtime application event.
- Relaunched the corrected Release build and captured the complete editor window.
- Selected K7 in the physical map and confirmed the K7 binding row followed.
- Release build completed with zero warnings/errors; self-tests passed 24/24.

### Unverified

- None for the reproduced constructor-order failure.
## 2026-07-17: Action drag/drop aborted the WinForms startup

### 症状

The first Instrument Workbench test launch showed a .NET unhandled-exception
dialog before the editor appeared: DragDrop registration failed.

### 原因

The previous top-level WinForms entry point did not explicitly mark the UI thread
as STA. Registering the new OLE drag source/targets requires a single-threaded
apartment.

### 修正内容

Converted the entry point to `Program.Main`, added `[STAThread]`, and retained the
same configuration load and form startup sequence.

### 再発防止

Any WinForms feature using OLE, clipboard, common dialogs, or drag/drop must keep
the entry point STA. The UI acceptance plan now includes a Release launch plus an
actual action-card drop.

### 確認手順

- Release build completed with zero warnings and errors.
- The app opened without the exception dialog.
- Dragging selected MO(1) onto K3 applied the action and selected K3.

### 未確認

Roller invalid-drop highlighting is not yet implemented; invalid actions remain
blocked or rejected by the existing capability/validator path.

## 2026-07-17: Header overflow button clipped and Live state shifted vertically

### Symptom

The left edge of the `•••` overflow button was clipped in the header action row.
When Live mode changed to Stop, that button also appeared slightly higher than
the adjacent Undo, Redo, and Revert buttons.

### Cause

The six controls and their WinForms margins required more width than the fixed
420-pixel action column provided. Live also used the native flat `Button` while
its neighbors used the owner-drawn `InstrumentButton`, so their visual bounds
did not share the same rounded inset.

### Fix

Expanded the action column to include all control margins, converted Live to the
same fixed-size `InstrumentButton`, and standardized its state labels to
`Live`, `Stop`, and `Reset`.

### Prevention

Header action groups must budget for control margins as well as declared widths.
Stateful controls in one visual row must keep a stable size and drawing class.

### Verification

- Release build completes with zero warnings and errors.
- Dependency-free self-tests pass.
- The running editor shows the complete overflow button and an aligned Live button.

### Unverified

- Visual confirmation of the Stop and Reset labels during a hardware Live session.

## 2026-07-17: Combo member edit caused recursive UI reload

### Symptom

After creating a Combo and changing its two physical member keys, the Combos
workspace could clear most controls, become unresponsive, or terminate.

### Cause

`LoadSelectedCombo()` rebuilt the checked Layer Scope list. Adding a checked item
raised `ItemCheck`, whose deferred callback ran after `_comboEditorLoading` had
returned to false. The callback committed unchanged metadata, reloaded the Combo,
rebuilt the checked list, and scheduled the same callback again.

### Fix

Ignore `ItemCheck` before scheduling its deferred commit while the Combo editor is
loading. User-originated checks still defer until the control has applied its new
checked state and then create one normal mutation.

### Prevention

Any WinForms event that uses `BeginInvoke` must test the editor-loading guard
before scheduling, not only inside the deferred callback.

### Verification

- Release build completes with zero warnings and zero errors.
- Deterministic self-tests pass.
- Visual regression: create a Combo, change K1+K2 to K3+K4, then wait; the
  workspace remains populated and responsive.

### Unverified

- Physical ACK05 Combo timing remains a separate hardware test.

## 2026-07-21: Compact header clipped workspace navigation

### Symptom

At 1081 x 741, `Combos` was only partly visible beneath the file `•••` button and
`Profiles` disappeared. The file menu did not contain workspace navigation.

### Cause

The header reserved fixed widths for brand and document/output actions, then placed
four workspace pills in the remaining non-wrapping FlowLayoutPanel. Their combined
width exceeded the compact navigation column, so WinForms clipped them individually.

### Fix

The navigation container now compares its client width with the measured pill
widths and margins. When space is insufficient it hides all pills as one group and
shows a dedicated current-workspace selector with all four checked menu choices.
Wide layouts restore the original pills and selected state.

### Prevention

Late-stage UI fixes must define evidence, goals, non-goals, risks, responsive/DPI
behavior, rollback, and acceptance checks before code changes. Responsive groups
must fail over atomically; a clipped control cannot be treated as navigation.

### Verification

- Release build: zero warnings/errors.
- Deterministic self-test: 40/40 passed.
- Current display scale, 1081 x 741: compact selector visible with no overlap.
- Keymap, Profiles, and Combos navigation passed; active menu check/label followed.
- Maximizing restored all four pills with Combos selected.
- Output remained OFF and resizing/navigation did not alter configuration state.

### Unverified

125%, 150%, and 200% system DPI remain unverified on a real scaled desktop.
