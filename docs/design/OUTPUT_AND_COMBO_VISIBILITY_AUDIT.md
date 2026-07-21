# Output and Combo Visibility Audit

Date: 2026-07-21

Status: implemented UI slice; hardware Output ON state not exercised in this audit

## Audit scope

The Keymap Combo overlay and the header control formerly labeled `Live` / `Stop`.
The design goal is to keep the physical map readable and communicate immediately
whether the app can currently emit configured Windows input.

Evidence was collected from the isolated Release build. This document records the
observed states, deterministic coverage, and the remaining hardware-visible gap.

## Flow

1. **Keymap with Combo badges visible - healthy with mild density risk**
   - The current-layer count is visible as `Combos 1`.
   - The selected black pill communicates that overlays are shown.
   - Badge overlays preserve the direct route into the Combo editor.

2. **Hide Combo badges - healthy**
   - One click changes the control to `Combos OFF` and removes every badge.
   - Save and Undo states do not change.
   - The status explicitly says definitions and output are unchanged.

3. **Output OFF and safety prompt - healthy**
   - The header begins with `Output OFF`, which reads as a state rather than an
     ambiguous destination.
   - Activating it opens `Enable ACK05 output?` and names raw mode, Windows input,
     and the system areas that are not changed.
   - Declining leaves output OFF.

4. **Output ON / waiting / connected - implementation verified, hardware view pending**
   - The button uses the stable label `Output ON` for both waiting and connected.
   - The status bar owns `waiting for ACK05`, connected, release, and error copy.
   - Automated runtime tests cover start/stop and release safety; this audit did
     not authorize a new hardware output session merely to capture the ON state.

## Strengths

- Editor lifetime and synthetic-output lifetime now have separate language.
- Both new controls are one-click and use the existing monochrome selected state.
- Combo visibility is clearly presentation-only.
- Accessible names expose output state and current-layer Combo count.

## UX risks

- `Output ON` means the output engine is armed even while the device is absent;
  the status bar must therefore remain visible and name `waiting` explicitly.
- A user may hide overlays and forget that Combos still execute. The current
  status copy mitigates this; persistent hidden state is deliberately not stored
  in v1.

## Accessibility risks and limits

- The controls are keyboard-reachable and expose stateful accessible names in the
  inspected UI Automation tree.
- Screen-reader announcement quality after dynamic text changes and 125/150/200%
  display scaling still require manual checks.
- Screenshot inspection cannot prove physical key release; deterministic tests
  and the existing hardware checklist cover that boundary.

## Recommendation

Keep output OFF on launch and recovery. Do not equate an open editor with active
device output. If a future diagnostics panel is added, show connection state and
recent physical input there without changing this explicit output boundary.
