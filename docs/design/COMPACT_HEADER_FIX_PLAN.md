# Compact Header Fix Plan

Status: implemented and verified at the current display scale

Date: 2026-07-21

## Evidence

At the current display scale, resizing the Release app to 1081 x 741 leaves
`Combos` partially visible beneath the file `•••` button and hides `Profiles`.
The file menu contains New/Open/Save As/Validate and is not a workspace fallback.
The physical map, inspector, Action Palette, status bar, and Output OFF control
remain usable.

## Goal

Keep every workspace reachable and make the active workspace visible at all
supported widths without clipping or overlapping header controls.

## Non-goals

- Do not change workspace content or navigation semantics.
- Do not merge workspace navigation into the file `•••` menu.
- Do not change config data, Save, Undo/Redo, Revert, Output, device transport,
  or runtime behavior.
- Do not add a dependency or a new persisted preference.
- Do not claim 125/150/200% DPI verification from a current-scale resize test.

## Selected design

- At widths where all four workspace pills fit, keep the existing one-click
  `Keymap`, `Tap & Hold`, `Combos`, and `Profiles` controls.
- When the navigation container becomes narrower than the measured pill widths,
  hide all four pills as one atomic group and show one dedicated workspace button.
- Label the compact button with the active workspace plus a disclosure marker.
- Its menu contains all four workspaces, checks the active item, and calls the
  existing `ShowWorkspace` path.
- Base the breakpoint on the navigation container's available client width and
  actual control widths/margins, not a hard-coded form width. This keeps the
  decision compatible with DPI scaling and future header width changes.

## Plan review

- Existing behavior: `ShowWorkspace` remains the single navigation state change.
- Persistence and Undo: compact/wide state is process-local layout only and never
  enters configuration or history.
- Output safety: no output/runtime path is touched; Output remains OFF during QA.
- Accessibility: the compact control has an explicit name/description and every
  workspace remains a normal menu item; no visually hidden control is the only
  pointer path.
- Responsive behavior: the mode switches atomically, so partial pills cannot
  remain beneath another control.
- Risk: repeated layout events could flicker. Mitigate by tracking the current
  mode and suspending layout only when the mode actually changes.
- Rollback: remove the compact button/menu and responsive mode method; no config
  migration or data restoration is required.

The review found no schema, safety, dependency, or irreversible change. The fix
can proceed without an additional product decision.

## Acceptance checks

1. Default 1420 x 940 shows all four workspace pills and the existing action row.
2. 1081 x 741 shows exactly one workspace selector; no partial tab or overlap.
3. The compact menu reaches all four workspaces and always displays/checks the
   current workspace.
4. Expanding the window restores all four pills with the active one selected.
5. Output remains OFF throughout the UI check.
6. Save dirty state, Undo/Redo availability, selected profile/layer, and Action
   Palette height do not change merely from resizing or navigating.
7. Release build completes with zero warnings/errors and all deterministic tests pass.
8. 125/150/200% DPI remains explicitly unverified until tested at those system scales.

## Verification result

Implemented on 2026-07-21. Release build completed with zero warnings/errors and
the deterministic self-test passed 40/40. At 1081 x 741 the header showed one
`Workspace: Keymap` selector with all four checked-menu choices. Profiles and
Combos opened through that menu, the label/check followed the active workspace,
and maximizing the window restored all four pills with Combos selected. Output
remained OFF and navigation/resizing did not change the config, Save/Undo state,
profile, layer, or Action Palette contract. System 125/150/200% remains unverified.
