# Worklog

## 2026-07-16: Project extraction and initial spike

- Confirmed the roBa repository was clean and left it unchanged.
- Confirmed no existing ACK05 project directory and no present ACK05 PnP device.
- Collected official XPPen, Microsoft, Linux kernel, and open-source driver sources.
- Chose USB/vendor-HID bitmask mode as the v1 input path.
- Created the independent .NET 8 solution, design documents, probe, and core
  state engine.
- Hardware verification remains blocked on connecting the ACK05.

## 2026-07-16: First connected-device probe

- Enumerated the connected ACK05 as `VID 28BD / PID 0202`.
- Verified one vendor collection at `FF0A:0001` with 32-byte input and output
  reports, plus separate keyboard, mouse, and digitizer collections.
- Received periodic battery reports in read-only mode.
- Sent the documented raw-mode command once and received its device response.
- Did not yet capture synchronized K1-K10 or roller reports; this remains the
  next hardware check.

## 2026-07-16: Synchronized K1, K2, and roller capture

- Started the raw-mode monitor before physical input and captured operations in
  the announced order: K1, K2, roller right, roller left.
- Verified K1 at byte 2 bit 0 and K2 at byte 2 bit 1, including zero-state
  release reports.
- Verified roller right as byte 7 value `01` (clockwise) and roller left as byte
  7 value `02` (counter-clockwise).
- No driver, HidHide, firmware, or pairing changes were made.
- K3-K10 remain the next synchronized hardware check.

## 2026-07-16: Direct Bluetooth enumeration and Raw Input diagnostic

- Confirmed direct Bluetooth HID-over-GATT enumeration with the same VID/PID and
  four standard collections, but no vendor-defined raw collection.
- Added explicit read-only collection monitoring and a Windows Raw Input
  diagnostic without adding third-party dependencies.
- Confirmed Windows Raw Input registration for the four ACK05 Bluetooth sources.
- Compared synchronized input with `PenTablet.exe` running and after the user
  manually stopped it. No device-attributable physical state report was obtained.
- Sent no Bluetooth output command and made no driver, HidHide, firmware, or
  pairing change.
- Recorded the bundled receiver phase as blocked because the owner does not have it.

## 2026-07-16: Complete wired key mapping verification

- Returned to wired USB with `PenTablet.exe` stopped and enabled the documented
  raw bitmask mode once.
- Captured synchronized down/up reports for K3-K10, completing the K1-K10 map.
- Verified K3-K8 as byte 2 values `04`, `08`, `10`, `20`, `40`, `80` and K9-K10
  as byte 3 values `01`, `02`.
- Observed repeated held-state reports for K8 and confirmed the transition
  tracker emits only one Down and one Up.
- Made no driver, HidHide, firmware, or pairing change.

## 2026-07-16: Simultaneous layer and disconnect verification

- Held K1 while tapping K2-K10 and confirmed every key routed Down/Up through
  Layer 1; a roller pulse also routed through Layer 1.
- Released K1 after the sequence and confirmed the router returned to Layer 0.
- Held K2 and unplugged wired USB. The probe observed repeated held-state reports,
  then reported the disconnected device and terminated.
- Reconnected USB and confirmed the same five collections, identifiers, usages,
  and report lengths returned normally.
- Synthetic release remains untested because output is not implemented yet.

## 2026-07-16: Reconnecting input service and reset boundary

- Added OS-independent report source, factory, sink, connection status, input
  session, and reconnecting service abstractions.
- Added a Windows HID source factory that opens only the ACK05 vendor collection
  and keeps raw-mode writes behind explicit `--enable-bitmask`.
- Added `watch` for reconnecting read-only monitoring and
  `watch --enable-bitmask` for the approved raw input path.
- Added deterministic fake-source tests covering disconnect, reconnect, shutdown,
  source disposal, Layer 1 key release, and non-cancelable reset delivery.
- Ran `watch --enable-bitmask` on hardware, held K2 during USB removal, observed
  K2 Up before Disconnected, and confirmed automatic Connected after cable restore.
- Verified idle Ctrl+C reaches Stopped; the held-key reset path was also covered
  by the deterministic fake-source test.
- Repeated the shutdown test with K3 physically held and observed K3 Up before
  Stopped, completing the hardware reset-order checks.

## 2026-07-16: Versioned action configuration model

- Added OS-independent schema v1 models for profiles, application match, two
  layers, bindings, logical keyboard chords, mouse wheel, and disabled actions.
- Reserved K1 as the momentary layer modifier and validated unique bindings,
  profile IDs, action payloads, chord length, and wheel range.
- Preserved unknown JSON properties through deserialize/serialize while rejecting
  unknown schema versions and enum names.
- Added five configuration test groups; all 13 self-tests passed.
- Defined the contract in `CONFIG_FORMAT.md`. File persistence, migration, Windows
  output, and GUI remain separate future work.

## 2026-07-16: Action engine and Windows output safety boundary

- Added logical output events and an action engine for keyboard chords and mouse
  wheel actions across Layer 0/1.
- Added shared-key reference counting, reverse chord release, pulse taps, profile
  change reset, and idempotent panic reset.
- Added a separate Windows project with virtual-key mapping, `SendInput`, pressed
  key registry, reverse release-all, and best-effort failure aggregation.
- Added an explicit configured `run` path requiring separate raw-mode and
  SendInput opt-ins, plus read-only config validation and a low-impact F24 smoke
  profile.
- Expanded deterministic coverage to 17 self-tests, including failed-release
  continuation and retry. Real SendInput hardware
  verification was the next step.
- Ran the explicit F24 smoke profile on wired ACK05 hardware. Verified Layer 0
  F24, Layer 1 Shift+F24, Layer 0 wheel +/-1, and Layer 1 wheel +2 output.
- Held K2 during USB removal and observed synthetic F24 Up before Disconnected,
  followed by automatic reconnect.
- Held K1+K2 during Ctrl+C and observed F24 Up then LeftShift Up before Stopped,
  completing the real-output shutdown release check.

## 2026-07-16: Foreground application profile selection

- Added validated default fallback and case-insensitive process matching with an
  optional `.exe` suffix.
- Rejected duplicate normalized application matches to avoid order-dependent
  profile selection.
- Added a read-only Windows foreground-process provider and 250ms monitor for
  configured `run`.
- Profile changes release active Core chords and the Windows safety registry
  before applying new bindings.
- Verified the live provider reported `chrome` and the disabled multi-profile
  smoke config automatically changed from default to chrome after connection.
## 2026-07-16: Keymap editor baseline

- Added `Ack05Enhanced.App`, a Windows Forms keymap editor for explicit JSON
  open/save.
- The editor shows profiles, process match, Layer 0 / Hold K1 Layer, K2-K10,
  roller clockwise/counter-clockwise, and action editing for disabled,
  keyboardChord, and mouseWheel.
- Save uses Core serialization and validation, writes through a temp file, and
  keeps `<config>.bak` when replacing an existing file.
- The editor is file-oriented only and does not start device monitoring, raw
  bitmask mode, or SendInput.

## 2026-07-16: File-dialog freeze regression fix

- Captured the editor in a genuine Windows `Not Responding` state immediately
  after the first Save click; no Save As window had been created.
- Switched Open and Save As to the built-in legacy Win32 dialog with a stable
  initial directory and directory restoration.
- Verified the first Save dialog appears, cancellation returns to a responsive
  editor, a temporary JSON file saves successfully, and `validate-config`
  accepts the saved schema.

## 2026-07-16: GUI Live mode

- Moved the reusable ACK05 HID discovery, raw-mode command, and reconnecting
  report source from Probe into the Windows adapter project.
- Added `Ack05LiveRuntime` with explicit lifecycle, foreground profile selection,
  immediate valid configuration replacement, SendInput output, and panic release.
- Added Start Live / Stop Live controls, connection status, one-time safety
  confirmation, and safe asynchronous shutdown to the WinForms editor.
- Confirmed the saved K2=A, K3=A, K4=C, and Layer 1 wheel +/-3 configuration
  produced real output before integration.
- Verified the integrated UI reached Connected, stopped with pressed-key release,
  and reconnected. Release build passed with zero warnings/errors and all 20
  self-tests passed.

## 2026-07-16: Notion development hub and K1 layer modes

- Linked the ACK05 Notion Lab item to a child implementation plan and recorded
  milestones, acceptance criteria, assumptions, and the local Git source of truth.
- Added backward-compatible global `layerSwitch` settings with Momentary, Toggle,
  and Tap Toggle / Hold Shift modes and a 100-1000 ms threshold.
- Generalized the Core layer router while preserving down-layer capture and reset
  to Layer 0.
- Added GUI controls for K1 mode and threshold. A semantic change during Live mode
  performs a controlled stop/start so held output and router state are cleared.
- Expanded deterministic coverage from 20 to 22 tests; Release build completed
  with zero warnings and errors.
- Visual WinForms smoke found the original three profile buttons and mode value
  were too narrow; changed both layouts and rechecked Momentary/TapToggleHold plus
  threshold enabled state in the rendered app.

## 2026-07-16: Device-first keymap map

- Added a dark ACK05 device map with roller, K1-K10, current layer/mode, compact
  action labels, and full-action tooltips.
- Kept the map stateless: selecting a physical control synchronizes the existing
  binding table and all labels reload from the current Core profile/layer.
- Displayed the roller center switch as pending because its raw report bit is not
  verified and therefore is not assignable yet.
- Increased the default/minimum window size and visually checked the rendered map.
- Clicked K5 in the rendered map and confirmed both the blue map selection and
  K5 DataGridView row followed; switching the tab repainted the header as Layer 1.

## 2026-07-16: Direct keyboard shortcut capture

- Added a modal capture flow that maps WinForms key events to logical Core keys,
  previews the chord, and requires both `Use Shortcut` and `Apply Binding`.
- Preserved captured key-down order and added modifier-first fallback for manual
  checklist combinations.
- Visually captured Ctrl+Shift+Z, verified the preview, applied it to K2, and
  confirmed the table order remained LeftControl + LeftShift + Z.

## 2026-07-16: Selectable K1-K10 layer control

- Added backward-compatible `layerSwitch.control`; missing values continue to
  select K1 and schema version remains 1.
- Added a GUI Trigger selector for K1-K10. Changing the trigger transfers the new
  trigger's actions to the previous trigger across every profile and both layers,
  so no binding is discarded and switching back restores the original placement.
- Updated the physical map, layer-tab title, validator, input service, and Live
  restart comparison to use the selected control instead of hardcoded K1.
- Release build passed with zero warnings/errors and all 24 self-tests passed,
  including non-K1 routing, K1-as-action, and dynamic reserved-control validation.

## 2026-07-16: Hardware-faithful device map

- Replaced the provisional 5x2 key grid using the ACK05 hardware reference:
  large left roller; K1-K3 / K4-K6; vertical K7; K8, wide K9, and K10.
- Added responsive owner-drawn key shapes while keeping selection, LAYER state,
  compact action summaries, tooltips, and roller direction editing functional.
- Increased the map region and window height so the physical silhouette remains
  readable, then visually verified K7 selection synchronizes with the binding row.
- Release build completed with zero warnings/errors and all 24 self-tests passed.

## 2026-07-16: Vial-style schema v2 multilayer keymap

- Replaced the global reserved layer control with per-key `MO(n)`, `TG(n)`, and
  Tap Toggle / Hold Layer actions. K1-K10 are all editable.
- Added 2-8 layers per profile, four-layer defaults, Transparent fall-through,
  highest-active-layer precedence, and captured-layer key-up safety.
- Added schema v1 migration that preserves Layer 0/1 bindings, converts the old
  layer switch into an action, and creates transparent Layers 2/3.
- Updated GUI tabs and action editor with target layer, threshold, and safe layer
  add/remove controls. Updated configured Probe and GUI Live pipelines.
- Verified Release build with zero warnings/errors and 28/28 deterministic tests,
  including end-to-end `K2=A` and `K1+K2=B` Live routing.
