# Test Plan

## Automated without hardware

```powershell
dotnet build .\Ack05Enhanced.sln -c Release
dotnet run --project .\tests\Ack05Enhanced.SelfTest -c Release
dotnet run --project .\src\Ack05Enhanced.Probe -c Release -- decode 02-F0-03-00-00-00-00-01
```

Expected:

- Build succeeds with zero warnings.
- Parser identifies every K1-K10 bit and both roller directions.
- K1 hold routes K2 down/up through Layer 1 even if K1 is released first.
- Malformed and non-raw reports are rejected.
- A simulated disconnect resets held Layer 1 inputs, disposes the source, and
  reconnects to a second source.
- Shutdown resets held input using a non-cancelable sink notification.

## Hardware phase A: wired USB

Preconditions:

- Save/export current XPPen settings if needed.
- Exit the XPPen tray app/driver process manually.
- Do not install HidHide yet.
- Keep a physical keyboard/mouse available.

Steps:

1. Connect ACK05 by USB-C cable.
2. Run `probe list`; record path hash, usage page, usage, input/output lengths.
3. Run `probe monitor` and press each key once. Confirm no device write occurs.
4. Stop monitor.
5. Run `probe monitor --enable-bitmask` after reading the confirmation text.
6. Press/release K1-K10 individually, then K1+each other key.
7. Rotate one detent clockwise and counter-clockwise.
8. Disconnect while K2 is held; confirm probe exits or reports disconnect cleanly.

Pass criteria:

- One vendor collection matches `FF0A/0001`.
- Raw report has marker `02 F0` and bit positions match the research table.
- Every physical key emits down and up.
- Wheel direction is consistent.
- No firmware, pairing, or XPPen profile is changed after reconnect.

## Hardware phase B: bundled receiver

Repeat phase A with the included receiver. Compare device path, collection count,
usage, report length, latency, and reconnect behavior. The Linux kernel source says
the receiver uses the same PID, but Windows behavior remains unverified. The current
device owner does not have the bundled receiver, so this phase is blocked on hardware.

## Hardware phase C: direct Bluetooth

Do not send the USB raw-mode command automatically. First record enumeration and
read-only reports. If no usable state report exists, capture the official driver's
traffic or defer support. Keep the BT transport feature-flagged until confirmed.

Observed on 2026-07-16:

| Usage page:usage | Input | Output | Raw Input type |
|---|---:|---:|---|
| `0001:0002` | 8 | 0 | mouse |
| `0001:0002` | 8 | 0 | mouse |
| `0001:0006` | 8 | 0 | keyboard |
| `000D:0002` | 10 | 0 | HID/digitizer |

- The Bluetooth paths use the HID-over-GATT service UUID and retain
  `VID 28BD / PID 0202`.
- No `FF0A:0001` vendor collection or writable output report is exposed.
- Direct read-only `CreateFile` access was refused for all four standard
  collections by the Windows input stack.
- Windows Raw Input lists all four ACK05 collections.
- With `PenTablet.exe` running, synchronized operations produced keyboard events,
  but they were not attributable to the `28BD:0202` Raw Input device; some had no
  readable source device.
- After the user manually stopped `PenTablet.exe`, K1/K2/roller synchronization
  and three additional K2 taps produced no ACK05-attributable Raw Input event.
- No raw-mode command was sent over Bluetooth. No driver, HidHide, firmware, or
  pairing setting was changed by the probe.

Result: direct Bluetooth remains deferred because this path does not currently
provide the physical down/up identity required for momentary layers.

## Output phase

After SendInput exists:

- K1 down + K2 tap emits only Layer 1 binding.
- K1 up before K2 up still releases Layer 1 binding.
- Disconnect releases all synthetic modifiers.
- Panic release works while another app is focused.
- Elevated target apps show the documented UIPI limitation.
- XPPen driver coexistence does not cause unnoticed double input.

## Unverified today

- ACK05 enumeration and vendor collection access were verified on 2026-07-16.
- The known raw-mode command was accepted and returned a response.
- K1-K10 and both roller directions are verified with synchronized physical input.
- Direct Bluetooth enumeration and Raw Input behavior are verified, but no usable
  physical state report was obtained.
- No synthetic keyboard output is implemented yet.

## Hardware observation: 2026-07-16

The connected ACK05 exposed five HID top-level collections:

| Usage page:usage | Input | Output | Role |
|---|---:|---:|---|
| `FF0A:0001` | 32 | 32 | vendor/raw candidate |
| `0001:0002` | 8 | 0 | mouse collection |
| `000D:0002` | 10 | 0 | digitizer collection |
| `0001:0002` | 7 | 0 | second mouse collection |
| `0001:0006` | 8 | 0 | keyboard collection |

Read-only monitoring received periodic 32-byte battery reports beginning
`02 F2 01 3C 01`. Sending the documented 32-byte padded raw-mode command returned
`02 B0 04 01 ...`, confirming that the selected vendor collection accepted it.
No firmware update, pairing change, driver replacement, or HidHide configuration
was performed.

### Synchronized input observation

The follow-up monitor was started before the physical operations. With roughly
one second between operations, K1, K2, roller right, and roller left produced:

| Physical operation | Raw report fields | Decoder event |
|---|---|---|
| K1 down | byte 2 = `01` | layer -> 1 |
| K1 up | byte 2 = `00` | layer -> 0 |
| K2 down | byte 2 = `02` | L0 K2 Down |
| K2 up | byte 2 = `00` | L0 K2 Up |
| Roller right | byte 7 = `01` | RollerClockwise Pulse |
| Roller left | byte 7 = `02` | RollerCounterClockwise Pulse |

The next synchronized capture verified the remaining keys:

| Physical operation | Raw report fields | Decoder event |
|---|---|---|
| K3 down/up | byte 2 = `04` / `00` | L0 K3 Down / Up |
| K4 down/up | byte 2 = `08` / `00` | L0 K4 Down / Up |
| K5 down/up | byte 2 = `10` / `00` | L0 K5 Down / Up |
| K6 down/up | byte 2 = `20` / `00` | L0 K6 Down / Up |
| K7 down/up | byte 2 = `40` / `00` | L0 K7 Down / Up |
| K8 down/up | byte 2 = `80` / `00` | L0 K8 Down / Up |
| K9 down/up | byte 3 = `01` / `00` | L0 K9 Down / Up |
| K10 down/up | byte 3 = `02` / `00` | L0 K10 Down / Up |

K8 also emitted repeated identical held-state reports while held; the transition
tracker correctly emitted only one Down and one Up. Together, the two captures
verify the complete public-source key and roller mapping on this ACK05 over wired
USB.

### Simultaneous layer and disconnect observation

With K1 held continuously, tapping K2-K10 produced combined bitmask reports and
the router emitted `L1` Down/Up pairs for every key. A clockwise roller pulse was
also routed through Layer 1. Releasing K1 after all taps returned the router to
Layer 0.

In a separate run, K2 was held while the USB cable was removed. The probe received
K2 Down plus repeated held-state reports, then reported that the device was no
longer connected and exited with an error status. This satisfies the current
probe disconnect criterion. Synthetic release on disconnect remains part of the
future output phase because no SendInput adapter exists yet.

After reconnecting the USB cable, `probe list` again found the same five
collections with the same VID/PID, usages, and report lengths. No firmware or
profile change was observed.

## Reconnecting service hardware phase

Run `probe watch --enable-bitmask`, then:

1. Confirm Connected and K1/K2 Layer 1 events.
2. Hold K2 and unplug USB.
3. Confirm a routed `L1 K2 Up` reset event is printed before
   Disconnected status. No SendInput output exists yet.
4. Reconnect USB without restarting the process.
5. Confirm Connected appears again and raw input resumes.
6. Press Ctrl+C while a key is held and confirm the routed Up appears before
   Stopped status.

Observed on 2026-07-16:

- `watch --enable-bitmask` entered Waiting, then Connected on wired USB.
- K2 was held while USB was removed. Output order was `L0 K2 Down`, `L0 K2 Up`,
  Disconnected, then Waiting. The Up reset therefore preceded reconnect handling.
- Reconnecting the cable produced Connected without restarting the process, and
  the documented raw-mode opt-in was applied by the new connection source.
- Ctrl+C with no key held produced Stopped.
- With K3 physically held, Ctrl+C produced `L0 K3 Up` before Stopped. The user
  kept K3 held until both lines were observed, verifying the shutdown reset order
  on hardware.

## Configuration model automated phase

Run `dotnet run --project .\tests\Ack05Enhanced.SelfTest` without hardware and
confirm the configuration tests pass. They cover:

1. Default profile and four layers serialize and deserialize.
2. Keyboard chord key order and signed mouse-wheel steps survive round trip.
3. Unknown properties at root, profile, and action levels survive round trip.
4. A future `schemaVersion` is rejected.
5. K1 bindings are allowed; duplicate bindings are rejected before serialization.

Observed on 2026-07-16: all 13 tests available at that phase passed. No configuration file was read
or written, and no device command or synthetic input was sent during this phase.

## Action and SendInput phase

Automated checks verify shared modifier reference counting, reverse chord
release, roller pulse tap behavior, mouse-wheel events, idempotent reset, duplicate
Windows key-down suppression, reverse panic release, best-effort continuation after
one release failure, and successful retry. These use a fake Windows injector and
must not call `user32!SendInput`.

For the explicit hardware smoke test, run:

```powershell
dotnet run --project .\src\Ack05Enhanced.Probe -- run `
  --config .\examples\hardware-output-smoke.json `
  --enable-bitmask --allow-sendinput
```

Then verify Layer 0 K2 produces F24 Down/Up, K1+K2 produces Shift+F24 in Layer 1,
roller directions produce signed wheel events, disconnect while K2 is held emits
F24 Up before reconnect, and Ctrl+C while K1+K2 is held releases F24 then Shift.
The example deliberately uses F24 to minimize application side effects. Keep a
separate keyboard available.

Observed on 2026-07-16:

- Layer 0 K2 produced `F24 Down` then `F24 Up` with successful SendInput calls.
- Holding K1 and tapping K2 produced `LeftShift Down`, `F24 Down`, `F24 Up`, then
  `LeftShift Up` through Layer 1.
- Layer 0 roller directions produced wheel `+1` and `-1`. An additional Layer 1
  clockwise pulse produced wheel `+2` while the shutdown chord was held.
- Unplugging USB while K2 was held produced routed K2 Up and synthetic F24 Up
  before Disconnected, then the process reconnected without restart.
- After reconnect, Ctrl+C while K1+K2 were physically held produced routed Layer 1
  K2 Up, synthetic F24 Up, synthetic LeftShift Up, and then Stopped/panic complete.
- No driver, HidHide, firmware, pairing, or profile setting was changed.

## Application profile selection phase

Automated tests verify default fallback, case-insensitive matching, optional
`.exe` normalization, and rejection of duplicate normalized process matches.
`probe foreground` provides a read-only check of the current Windows foreground
process. During configured `run`, changing focus to a matched process must print
`profile -> <id>`; changing to an unmatched process must return to the default.
Any active chord must be released before the profile-change line. Hardware focus
switch verification can use `examples/application-profile-smoke.json`, whose
bindings are all disabled.

Observed on 2026-07-16: `probe foreground` returned `chrome`. Running the disabled
two-profile smoke config connected with `default`, then printed
`profile -> chrome (Chrome foreground test)` without user input. Ctrl+C completed
panic release and stopped normally. No synthetic key or wheel event was configured
or sent in this check.
## Keymap editor phase

### Automated checks

- `dotnet build .\Ack05Enhanced.sln -c Release` must build
  `Ack05Enhanced.App` with zero warnings and errors.
- Existing self-tests must still pass because the editor uses the same Core
  schema and validator.

### Manual smoke

1. Run `dotnet run --project .\src\Ack05Enhanced.App`.
2. Confirm a default profile appears with Layer 0 and Hold K1 Layer tabs.
3. Select K2, set `KeyboardChord`, choose `F24`, and apply the binding.
4. Select `RollerClockwise`, set `MouseWheel` with `1`, and apply the binding.
5. Save as a temporary JSON file.
6. Run `dotnet run --project .\src\Ack05Enhanced.Probe -- validate-config <saved.json>`.
7. Reopen the saved file in the editor and confirm the same bindings are shown.

### File-dialog freeze regression

1. Start the editor without a configuration path.
2. Apply any valid binding and click `Save` for the first time.
3. Confirm the Save As dialog appears without Windows marking the editor as
   unresponsive.
4. Cancel the dialog and confirm the editor remains responsive.
5. Open Save again, write a temporary JSON file, and validate it with
   `validate-config`.

### GUI output mode

1. Open a valid configuration in the editor and confirm the initial button is
   `Output OFF`.
2. Click `Output OFF`, read the raw-mode/SendInput warning, and explicitly
   confirm.
3. Confirm the button becomes `Output ON` and the status changes through waiting
   to `Output on; ACK05 connected`.
4. Verify configured K2-K10 and Layer 0/1 roller output in a safe test window.
5. Change one valid binding with one action-card click; confirm the new action is
   used without Save or an output restart.
6. Hold a configured chord and click `Output ON`; confirm all synthetic keys are
   released and the button returns to `Output OFF`.
7. Turn Output ON again, close the editor, and confirm shutdown completes without a
   held synthetic key.

The editor must not open the ACK05 HID collection, enable bitmask mode, call
SendInput, install drivers, configure HidHide, or change XPPen settings unless
the user explicitly switches to Output ON. Active output may only send the documented
raw-mode command and configured SendInput actions.

## Vial-style multilayer keymap (schema v2)

### Automated checks

1. Default schema v2 has four layers and Layer 0 K1=`MO(1)`.
2. Layer 0 K2=A routes on Layer 0 without K1.
3. K1=`MO(1)` plus Layer 1 K2=B routes K1+K2 on Layer 1.
4. Releasing K1 before K2 still emits K2 Up through captured Layer 1.
5. `TG(2)` latches/unlatches Layer 2; key-up is inert.
6. Transparent on Layer 2 falls through to the active lower binding.
7. Momentary Layer 3 wins over toggled Layer 2 while both are active.
8. Tap Toggle / Hold Layer distinguishes short solo tap from hold/chord.
9. Reset releases held output, clears all active layers, and returns Layer 0.
10. Schema v1 Momentary/Toggle/Tap-Hold migrates to per-key layer actions.
11. Future schema versions, self-targets, invalid targets, and momentary roller
    actions are rejected.
12. GUI output fake-transport flow produces K2=A and K1+K2=B end-to-end.

### GUI smoke

1. Start the editor and confirm Layer 0-3 selectors plus the adjacent `+` / `−` controls.
2. Layer 0: set K2 KeyboardChord=A and K1 MomentaryLayer target Layer 1.
3. Layer 1: set K1 Transparent and K2 KeyboardChord=B.
4. Save, reopen, and confirm the four assignments and target layer.
5. Add Layer 4, assign K3=`TG(4)`, and confirm Layer 4 appears in every target list.
6. Attempt to remove Layer 4 while referenced and confirm deletion is blocked.
7. Remove K3's layer action, then remove Layer 4 successfully.
8. Resize to 1080x740 and confirm selectors, physical map, inspector, action dock, and status
   remain reachable.

### Hardware acceptance

Use a safe test binding such as A/B or F23/F24. Verify K2 alone produces the
Layer 0 action, holding K1 while tapping K2 produces the Layer 1 action, and both
keys release cleanly. Then verify one arbitrary non-K1 `MO`, one `TG`, and one
Tap Toggle / Hold action. This is the remaining check that requires user timing
and physical input.

#### 2026-07-17 hardware result: K1 `MO(1)`

- Transport: ACK05 connected over Bluetooth; the Windows vendor HID collection
  was opened by Live mode without changing drivers, HidHide, firmware, or pairing.
- Test map: Layer 0 K1=`MO(1)`, Layer 0 K2=`A`, Layer 1 K1=`Transparent`,
  Layer 1 K2=`B`.
- Physical sequence: tap K2, hold K1, tap K2, release K1.
- Observed output in a blank Notepad document: `ab`.
- Result: passed. K2 routed to Layer 0 normally and to Layer 1 while K1 was held.
- Cleanup: `Stop Live` reported `Live mode stopped; pressed keys released.`
- Remaining hardware checks: arbitrary non-K1 `MO`, `TG`, and Tap Toggle / Hold.

## Device-first keymap UI

1. Confirm the rendered device map matches the hardware reference: large roller
   at left; K1-K3 above K4-K6; vertical K7; K8, wide K9, and K10 below.
2. Click K2-K10, CCW, and CW; confirm the hardware selection and right inspector
   follow each click.
3. Change a binding and confirm the compact map label and tooltip update.
4. Switch Layer 0/1 and confirm the map repaints from that layer without changing
   profile data.
5. Resize to the minimum 1080x740 and confirm the map, inspector, action dock,
   status, and layer controls remain reachable.

## Keyboard shortcut capture

1. Select K2 and click `Capture Keyboard Shortcut...`.
2. Press Ctrl+Shift+Z and confirm the dialog previews
   `LeftControl + LeftShift + Z`.
3. Click `Use Shortcut`; confirm `KeyboardChord` is selected and the inspector plus
   device map update immediately.
4. Click Undo once and confirm the previous binding returns; Redo restores the chord.
5. Cancel a second capture and confirm the existing edit is unchanged.
6. Capture an unsupported key and confirm it is ignored without closing the dialog.

## Instrument Workbench Action Library

1. Select a Disabled K2. Confirm Common is selected, Disabled is black, and the
   inspector names K2 plus Disabled.
2. Select Normal Keys and click A once. Confirm A replaces Disabled immediately,
   K2 shows A, Undo enables, and Save shows the dirty marker.
3. Switch among Common, Layers, Navigation, Normal Keys, Modifiers, Function Keys,
   Numpad, Media, and Other Keys. Confirm the card set follows the category.
4. Select A and Left Control. Confirm the inspector shows
   `Keyboard: LeftControl + A` and both cards remain selected in their categories.
5. Select Toggle (TG). Confirm the keyboard chord is cleared, TG is black, and the
   Target layer field appears while keyboard-only controls move under Advanced.
6. Click Momentary (MO) once. Confirm TG is replaced directly by MO without an
   intermediate empty selection.
7. Select a roller direction. Confirm MO and Tap / Hold are blocked, while Mouse
   Wheel and TG remain selectable.

## Reusable Tap & Hold (schema v3)

### Automated

1. Round-trip a definition and `tapHoldId` binding through schema v3 JSON.
2. Load schema v2 and confirm legacy bindings remain unchanged with empty definitions.
3. Verify a short `Tap=B / Hold=Shift` press emits a B pulse only.
4. Verify term expiry holds Shift until the physical owner key is released.
5. Verify `Tap=B / Hold=MO(1)` resolves Hold before an interrupting key and routes
   that key through Layer 1 for Hold-on-press.
6. Compare Hold-on-tap and Timeout-only against the same physical sequence.
7. Reset while pending and confirm no Tap fires; reset after Hold confirmation and
   confirm a matching key-up plus Layer 0 restoration.
8. Run the Live fake transport and confirm the 10 ms timer produces matching
   Shift Down/Up without requiring a second physical input.

### GUI smoke

1. Open Tap & Hold and create `th0`; confirm the default is Tap B / Hold Left Shift.
2. Select TAP, choose a keyboard action from the shared library, and confirm only
   Tap-compatible actions remain enabled.
3. Select HOLD, choose MO(1), and confirm Toggle/Mouse Wheel are blocked while
   Target Layer appears.
4. Change the term and all three Interrupt choices; Undo and Redo each complete edit.
5. Return to Keymap, select K2, then use `Assign to selected key`; confirm K2 shows `TH(th0)`.
6. Attempt to delete `th0` while assigned and confirm deletion is blocked with a reference count.
7. Switch to Output ON only after the existing warning and verify short K2 emits
   Tap while a held/interrupted K2 applies Hold. Switch Output OFF and confirm no
   held output remains.

## Implemented Combo runtime and editor

1. Create `K1 + K2 -> Enter`. Confirm it initially uses the profile Combo Term and
   shows only the basic fields.
2. Override the term to 40 ms. Confirm one committed edit creates one Undo entry,
   one active-output configuration replacement, and no file write.
3. Confirm the numeric controls constrain Combo Term to 20-200 ms and Hold Term
   to 50-2000 ms; invalid JSON must fail validation before Output ON or Save.
4. Set Key Order to `K1 then K2`. Confirm K1 then K2 matches while K2 then K1
   replays the individual bindings.
5. Set Trigger to Tap only and Hold Term to 150 ms. Confirm a quick chord fires the
   Combo and a held chord replays the individual bindings without duplicate events.
6. Set Trigger to Hold together. Confirm release before Hold Term replays the
   individual bindings and holding through the term fires the Combo.
7. Compare First member released with All members released. Confirm the output Up
   occurs at the selected boundary and never releases an output still owned by a
   different binding.
8. Confirm a third key arriving before timer confirmation fails closed and replays
   the buffered member bindings. The measured output diagnostic remains follow-up work.
9. Switch Output OFF or disconnect while a held Combo output is active. Confirm matching
   key-up/MO release occurs and no member binding fires afterward.

Automated self-test coverage includes schema round-trip and validation, Press
match/fallback, Tap-only, Hold-together, reset release, and Live 10 ms timer
confirmation, plus all-member MO release. Current total: 40/40 deterministic tests.

User hardware result, 2026-07-20: the requested output checklist passed, including
MO and TG layer behavior, non-K1 layer switching, Tap & Hold tap/hold output,
two-key Combo recognition/fallback, immediate output configuration replacement,
Output OFF/close release safety, and absence of stuck modifiers. The header now
uses Output OFF/ON while retaining the verified runtime behavior.

## Instrument Workbench UI acceptance

1. Launch the Release app and confirm the workbench shows the hardware map,
   selected-key inspector, action dock, profile selector, and Layer 0-3 controls.
2. Select K2, open Normal Keys, and click A once. Confirm K2 and the inspector
   both show A immediately, Save becomes dirty, and no file is written.
3. Click Undo and confirm K2 returns to Disabled. Click Redo and confirm A returns.
4. Save, then Undo. Confirm history is retained and the document becomes dirty
   relative to the new saved baseline.
5. Make another edit, click Revert, then Undo. Confirm Revert returns to the last
   Save and Undo restores the pre-Revert working draft.
6. Select MO(1) and drag its card onto K3. Confirm K3 becomes MO(1), the inspector
   follows K3, and one Undo restores its previous binding.
7. Switch through Tap & Hold, Combos, Profiles, and Keymap. Confirm navigation is
   functional and returning to Keymap preserves the draft.
8. Expand/collapse Advanced and confirm the short ease-out animation does not
   block selection, Save, or window close.
9. Switch to Output ON only after the existing warning, change one valid action, and
   confirm the runtime hot-swaps without Save.
10. Close while output is active and confirm the existing panic-release path still
    completes before process exit.
11. Confirm the `•••` overflow button is fully visible and that the Output button
    keeps the same position and dimensions when its state changes between OFF and ON.
12. Open Normal Keys at the default 1420 x 940 window size. Confirm at least two
    complete card rows are visible and no card is cut by the action dock footer.
13. Drag the horizontal divider above the Action Palette up and down. Confirm the
    library changes height continuously, its scrollbar remains usable, and the
    physical map and right inspector remain available above it.
    Confirm only the short centered rounded handle is visible: the draggable area
    must not introduce a full-width rule or another theme color.
    Leave the palette at a non-default height, open Profiles, then return to
    Keymap. Confirm the chosen height and resize handle are still present.
14. Increase the window height after launch. Confirm the editor height remains
    stable and the added vertical space is given to the Action Palette. Shrink to
    the minimum size and confirm neither split panel collapses.
15. Open Tap & Hold or Combos with no definition selected. Confirm every action is
    unavailable and the footer explains what must be created or selected. Create a
    Combo and confirm the palette becomes available again.
16. In a four-layer profile, select a Combo and scroll its inspector to Layer
    scope. Confirm Layer 0-3 are all visible without scrolling inside the checklist.
17. Create `K1 + K2 -> Enter` for Layer 0 and return to Keymap. Confirm a compact
    `Enter` / `L0` badge appears near the pair without covering the key labels.
    Hover or keyboard-focus it and confirm K1/K2 plus their connector are emphasized.
18. Activate the badge and confirm Combos opens with the same definition selected.
    Change to a layer outside its scope and confirm the badge is not shown.
19. From Combos, scroll the right inspector, then return to Keymap. Confirm
    `KEY SETTINGS` and the selected-key caption are fully visible at the top.
20. Use Tab and Shift+Tab without the mouse. Confirm every owner-drawn navigation
    button, K1-K10 key, enabled action card, and Combo badge has a visible
    monochrome focus cue; unavailable action cards are skipped.
21. Focus the roller, press Left and Right Arrow, and confirm the selected binding
    changes to `Roller CCW` / `Roller CW` without changing configuration data.
22. Open Tap & Hold. Confirm the current-layer ACK05 map remains visible between
    the definition list and settings, and the selected definition reports exact
    references such as `Used by L0 K7`.
23. Confirm keys using the selected Tap & Hold definition are highlighted on the
    map and labels resolve to Tap/Hold meaning such as `Bksp/MO(1)`, with the full
    meaning available in the map summary and tooltip.
24. Click a different K1-K10 target in Tap & Hold. Confirm the map selection,
    assignment-target copy, and always-visible assignment button update without
    changing configuration until the assignment button is activated. Select a
    roller direction and confirm assignment is disabled with a pulse-only reason.
25. Repeatedly switch Keymap, Tap & Hold, and Combos, then select several keys and
    action cards. Confirm the Action Palette does not visibly blank/repopulate and
    unchanged map keys do not replay selection motion. Record any remaining pause
    for the development-only timing follow-up in the map-first plan.
26. Compare K1-K10 at both the default and a compact map size. Confirm K9 and K7
    use the same key-label and action-label scale as the square keys; their wider or
    taller geometry must not enlarge the text.
27. Show a current-layer Combo badge while either member key is selected. Confirm
    the badge remains a distinct white capsule with a black outline and black text,
    including where it overlaps the selected key's black fill.
28. With current-layer Combos present, confirm the compact control above Keymap
    reads `Combos <count>` and the badges are visible. Click it once; confirm it
    reads `Combos OFF` and all badges disappear. Click again and confirm they
    return. Neither click may dirty the document, add Undo history, or disable
    physical Combo output.
29. Confirm the header starts at `Output OFF`, changes to `Output ON` while waiting
    or connected, and returns to `Output OFF` after one click. The button must stay
    in the same position and the status bar must independently name waiting,
    connected, released, and error states.
30. Resize to 1081 x 741. Confirm the four workspace pills are replaced atomically
    by one active-workspace selector, with no partial pill or overlap. Open its menu,
    confirm all four workspaces are present and the current one is checked, navigate
    to Profiles and Combos, then maximize and confirm all four pills return with
    the active workspace selected.

Verified on 2026-07-21 with the isolated Release build: the visible/hidden Combo
states, current-layer count, unchanged Save/Undo state, accessible names, Output
OFF initial state, and revised first-use warning passed. Output ON waiting/connected
visual state remains a hardware/manual follow-up; runtime lifecycle and release
safety remain covered by the 40 deterministic tests and the 2026-07-20 hardware run.

Verified on 2026-07-18 with the isolated Release build: steps 17-21 passed at
the default 1420 x 940 size.

Verified on 2026-07-21 at the current display scale: the window was resized to
1081 x 741. The physical map, inspector, resizable/scrollable Action Palette,
fixed status, and Output OFF state remained reachable. Compact-width header
navigation failed: `Combos` remained partially visible beneath the `•••` button
and `Profiles` was hidden; `•••` contains file commands and is not a workspace
fallback. The window was maximized after the check. 125/150/200% system scaling
remains unverified because the supported desktop API could not target Display
Settings or pass a settings-page launch argument; no system-wide setting was
changed.

Fix verification on 2026-07-21 with the Release build: step 30 passed. The compact
selector showed `Workspace: Keymap`, exposed all four checked-menu choices, opened
Profiles and Combos, and returned to four direct pills with Combos selected after
maximize. Output remained OFF. Save/Undo state, profile/layer selection, and the
Action Palette contract were unchanged.

200% DPI verification on 2026-07-21 with the user's active Windows display
setting and the Release build: the maximized app used the compact workspace
selector, and Keymap, Tap & Hold, Combos, and Profiles all remained reachable.
The physical map, selected-key inspector, resizable Action Palette, layer/profile
controls, K9 typography, white/outlined Combo badges, status area, and Output OFF
boundary remained present. The output runtime was not enabled and no configuration
was saved. 125% and 150% remain unverified.

## Working draft recovery

1. Start without a configuration path, create `K1 + K2 -> Enter`, and do not Save.
2. Wait at least 650 ms, close normally, and restart. Confirm startup offers to
   Restore or Discard the working draft and identifies it as unsaved.
3. Choose Restore. Confirm the window is dirty, Output remains off, and the Keymap
   shows the restored Enter/L0 Combo badge.
4. Choose Revert to Saved. Confirm the Combo disappears and the window becomes clean.
5. Close and restart again. Confirm no recovery prompt appears.
6. Repeat with a saved source file, modify that file externally, then attempt Save.
   Confirm silent overwrite is blocked and Overwrite / Save As / Cancel are offered.
7. Place invalid JSON at the recovery path. Confirm startup quarantines it and falls
   back to the saved/default configuration without enabling output.

Verified on 2026-07-18 with the isolated Release build: steps 1-5 passed. The
restored draft retained `K1 + K2 -> Enter`, Revert removed it, and the following
restart opened without a recovery prompt. External-change and corrupt-envelope UI
paths remain manual follow-up.
