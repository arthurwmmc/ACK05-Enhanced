# TODO

## Hardware / transport

- [x] USB vendor collection, battery report, raw-mode response
- [x] K1-K10 down/up and roller left/right mapping
- [x] simultaneous layer routing and disconnect reset
- [x] direct Bluetooth collection/Raw Input observation
- [ ] bundled USB receiver verification (receiver not available)
- [ ] direct Bluetooth transport that exposes device-attributable physical state

## Keymap editor

- [x] profile and foreground-process matching
- [x] hardware-shaped K1-K10/roller map
- [x] keyboard shortcut capture
- [x] explicit Open/Save/Validate and Output OFF/ON
- [x] schema v2 with 2-8 layers and schema v1 migration
- [x] Transparent, MO, TG, and Tap Toggle / Hold Layer on arbitrary keys
- [x] dynamic add/remove layer UI with reference-safe deletion
- [ ] layer names, duplicate layer, copy/paste
- [x] session undo/redo across Save, dirty baseline, and Revert to Saved
- [x] Local App Data draft recovery after abnormal exit
- [ ] default per-user configuration and startup load/save
- [ ] tray status and auto-start

## Vial-like expansion

- [x] collapsible categorized keycode/action palette with exclusive-action states
- [x] one-click replacement of the current action; gray only unsupported actions
- [x] drag the selected palette action onto physical K1-K10 targets with Undo
- [ ] richer invalid-drop highlighting/reasons for pulse-only roller targets
- [x] keycode/action palette search and category filter with a sticky physical map
- [ ] Recent/Favorites sections in the action dock
- [x] reusable Tap & Hold definitions and quick create/assign from the selected key
- [x] independent Tap and Hold actions with per-definition tapping term
- [x] `Tap=B / Hold=Shift` and `Tap=B / Hold=MO(1)` runtime slices
- [x] Hold-on-press / Hold-on-tap / Timeout-only interrupt modes
- [x] schema v3 Tap & Hold contract with unchanged schema v2 binding behavior
- [x] reusable exactly-two-key Combo definitions with visual member-key selection
- [x] Keymap combo badges/connectors and layer-scope labels
- [x] process-local Keymap Combo badge visibility toggle
- [x] responsive header: at compact width, move each unavailable workspace tab to
  a reachable navigation menu instead of clipping `Combos` or hiding `Profiles`
- [ ] strict-term Combo resolver follow-up: surface Tap/Hold timing warnings and verify same-report three-key fail-closed behavior on hardware
- [ ] Combo UI follow-up: output timing diagnostic
- [x] first Combo hardware test: `K1 + K2 = Enter`
- [ ] Double Tap and Tap + Hold slots after the Tap/Hold slice is stable
- [ ] `TO`, `OSL`, `LT`, `LM`
- [ ] true multi-tap `TT`
- [ ] macro editor and arbitrary tap action
- [ ] radial action menu: assign activation/forward/backward inputs, select 4-8 actions, execute on activation release
- [ ] native portable device/keymap package and explicit import-result contract
- [ ] second-device adapter experiment to validate the generic input boundary
- [ ] import/export and profile sharing

## Safety / distribution

- [x] choose public visibility and MIT license
- [x] create/connect the public GitHub repository and run CI from a clean checkout
- [x] ignore personal `ack05-config.json` and backup files; keep safe examples
- [x] GitHub Actions Windows Release build and 40/40 self-test workflow
- [x] INSTALL, USER_GUIDE, SECURITY, and release checklist drafts
- [ ] release packaging, screenshots, and release notes
- [ ] publish a Japanese note article after the first preview release is available
- [ ] optional HidHide manual setup and recovery guide
- [ ] signed/release packaging
- [ ] JSX/process launch allowlist
- [ ] MIDI/HTTP/OBS integrations
