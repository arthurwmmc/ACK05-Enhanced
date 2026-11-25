# Changelog

## Unreleased

### Changed

- Contributor guidance now lives in `CONTRIBUTING.md`; reproducible UI evidence
  is consolidated under `docs/design/UI_QA_REPORT.md`, while environment-specific
  working instructions are kept outside the published source tree.
- Preview-release blockers, near-term usability work, advanced bindings, and
  long-term device expansion are now separated in `ROADMAP.md`. Current user-facing
  documentation consistently calls the runtime boundary `Output OFF` / `Output ON`.
- The ambiguous `Live` / `Stop` header action is now the consistent stateful
  `Output OFF` / `Output ON` control. Output still starts OFF and still requires
  explicit first-use confirmation; waiting and connection details remain in the
  status bar.
- Keymap now has a one-click, process-local `Combos <count>` / `Combos OFF`
  visibility control. Hiding badges does not change Combo definitions or output.

### Fixed

- Output OFF and device disconnect now cancel pending Tap & Hold, Combo and
  Radial Menu gestures before disposing the input source. Already-injected keys
  are released and late reads from a canceled capture are discarded.
- Radial overlay rendering no longer blends antialiased edges against the
  transparency key, removing colored fringes around external labels and leaders.
- Radial pointer placement now keeps the complete overlay inside the active
  monitor work area, including when the editor is partly off-screen.
- Compact-width headers now replace all workspace pills atomically with a dedicated
  active-workspace menu, preventing clipped Combos/Profiles navigation beneath the
  separate `•••` file menu.
- Wide K9 and tall K7 keys now derive label sizes from their shorter dimension,
  keeping physical-key typography consistent across the map. Combo badges now use
  the same white surface and black outline as the keymap instead of merging into a
  selected black key.
- The Action Palette now preserves its user-selected height when returning from
  Profiles and reapplies its minimum height after a delayed/small initial layout,
  preventing the draggable palette area from disappearing.
- Workspace and key-selection updates no longer rebuild unchanged Action Palette
  cards or restart unchanged map-button animations, reducing avoidable UI work.

### Added

- Schema-v4 reusable Radial Menus with assignable activation/forward/backward
  inputs, 4/6/8 clockwise segments, hold threshold, pulse navigation step,
  pointer or monitor-center placement, and safe leaf-action validation.
- Combo-first, timer-driven radial routing with short-press replay, no-selection
  cancel, wraparound navigation, release-to-execute, and panic-safe cancellation.
- Dedicated Radial Menus workspace and focusless click-through overlay using a
  continuous monochrome wheel, external labels, Windows system icons, a compact
  center, solid selected contrast, safe preview, and multi-monitor clamping.
- `ACK05_ENHANCED_RECOVERY_PATH` test/development override for isolated visual QA;
  the normal Local App Data recovery location is unchanged.
- Schema-v4 `Open Target` action for launching an executable, associated file,
  folder, shortcut, or HTTP/HTTPS URL once through the Windows shell. The shared
  Action Palette supports normal bindings, Tap output, and Combo output; GUI
  launch remains behind Output ON and Probe launch has a separate opt-in flag.
- A reusable target picker with direct URL/path entry plus file and folder browse
  actions, non-fatal launch-error reporting, and v1-v3 configuration migration.
- Hardware-verified roller-center press input. The center switch is now a stateful
  `RollerPress` control with normal Down/Up semantics, editable keymap bindings,
  Tap & Hold assignment, and Combo membership. Existing configuration files load
  unchanged and receive a disabled center binding during normalization.

- Public-source README, MIT license, contribution guidance, and a documented
  follow-up to publish a Japanese note article after the first preview release.
- Map-first Tap & Hold workspace with a compact current-layer ACK05 map, direct
  K1-K10 assignment targeting, exact layer/key references, and resolved Tap/Hold
  labels instead of internal IDs.
- Visible keyboard focus cues and accessibility labels for owner-drawn navigation,
  physical keys, action cards, and Combo badges, plus Left/Right Arrow selection
  for roller CCW/CW bindings.
- Windows GitHub Actions workflow for clean Release builds and deterministic
  self-tests without ACK05 hardware.
- Installation, user, security, and release-checklist documentation for the
  source-build preview.
- Git ignore rules for personal root configuration, backup, and temporary files.
- Current-layer Combo badges on the Keymap, with member highlighting, connector
  feedback, layer scope, and direct navigation to the selected definition.
- Debounced Local App Data working-draft recovery with startup Restore/Discard,
  saved-baseline preservation, corrupt-envelope quarantine, and clean-state removal.
- Saved-file SHA-256 tracking that blocks silent overwrite after an external change
  and offers Overwrite, Save As, or Cancel.
- Schema-v3 exactly-two-key Combo definitions with profile/default timing,
  per-Combo override, order, Press/Tap-only/Hold-together triggers, layer scope,
  and first/all-member release policy.
- Combo-first resolver with strict buffering, timestamp-preserving fallback,
  active-layer snapshot, timer confirmation, and panic-safe held-output release.
- Combos workspace with map-based two-key selection and shared-palette output editing.
- Schema v3 reusable Tap & Hold definitions with independent Tap/Hold leaf
  actions, stable references, 100-1000 ms terms, and three interrupt modes.
- Timer-driven Tap & Hold routing for keyboard holds and MO layers, including
  panic-safe cancellation and schema-v2-compatible loading.
- Tap & Hold workspace for creating, editing, deleting, and assigning definitions
  through the shared action library.
- Instrument Workbench layout with a darker warm-gray canvas, hardware-first
  keymap, persistent selected-key inspector, and card-based action dock.
- Immediate in-memory assignment from action cards and inspector parameters,
  session Undo/Redo across Save, dirty-state indication, and Revert to Saved.
- Searchable categorized action cards plus drag-and-drop of the selected action
  onto a physical K1-K10 target.
- Vertically resizable Action Palette that receives additional window height,
  exposes multiple complete card rows, and keeps its own scrolling region. Its
  divider uses a small centered rounded handle instead of a full-width color band.
- Keymap, Tap & Hold, Combos, Radial Menus, and Profiles workspaces with dedicated
  editors for reusable behaviors and a shared action library.
- Ease-out hover/selection transitions and an animated Advanced inspector drawer.
- Unified collapsible Action Palette with direct Disabled/Transparent selection,
  grouped keyboard keys, and exclusive layer/device actions that gray out
  incompatible choices.
- Schema v2 Vial/QMK-style keymap model with 2-8 layers per profile, four-layer
  defaults, Transparent fall-through, and no globally reserved key.
- Per-key `MO(n)`, `TG(n)`, and Tap Toggle / Hold Layer actions for K1-K10, plus
  roller `TG(n)` support.
- Dynamic GUI layer tabs with reference-safe add/remove controls and target-layer
  action editing.
- Automatic schema v1 migration that turns the previous global layer switch into
  a Layer 0 action and adds transparent Layers 2 and 3.
- Profile-aware multilayer routing in both GUI Live and configured Probe output.
- Independent ACK05 Enhanced project and Git repository.
- Evidence-based ACK05 protocol and Windows input-path research.
- OS-independent raw report decoder and physical transition tracker.
- Configurable K1-K10 layer router with down-layer capture for safe key-up routing.
- Windows HID collection probe with read-only default behavior.
- Explicit read-only collection monitoring and Windows Raw Input diagnostics.
- OS-independent input session and reconnecting service with forced reset events.
- Windows HID report-source adapter and reconnecting `watch` command.
- Versioned profile/layer/binding JSON model with logical keyboard and wheel actions.
- Configuration validation and unknown-property round-trip preservation.
- OS-independent action engine with shared-key reference counting and reverse release.
- Windows SendInput keyboard/wheel adapter with a second pressed-key safety registry.
- Explicit configured `run` command and read-only `validate-config` command.
- Foreground-process profile selector with normalized `.exe` matching and safe
  release before profile changes.
- Windows Forms keymap editor for explicit JSON open/save, profile metadata,
  Layer 0 / Hold K1 Layer bindings, keyboard chord actions, and wheel actions.
- Atomic editor save with same-directory temp file replacement and `.bak`
  creation when replacing an existing config.
- Explicit GUI Live Start/Stop, connection status, immediate valid-config
  updates, and panic release on stop or window close.
- Shared Windows HID discovery/raw-mode adapter used by both Probe and GUI.
- Global K1-K10 layer control selector with Momentary, Toggle, and Tap Toggle / Hold Shift
  modes plus a configurable 100-1000 ms threshold.
- GUI layer-control/mode editor with binding-preserving trigger swaps and a safe
  Live stop/start when semantics change.
- Device-first ACK05 map matching the hardware geometry: large roller, 3x2 key
  cluster, vertical K7, K8, wide K9, and K10, with layer/mode context, compact
  action labels, full tooltips, and synchronized binding selection.
- Keyboard shortcut capture dialog with logical-key mapping, modifier capture,
  explicit confirmation, and preserved key-down order.
- Dependency-free self-test executable.

### Fixed

- Reset each workspace inspector to its first field when opened, preventing a
  previous Combo/Keymap scroll position from clipping the inspector heading.
- Use compact `Roller CW` / `Roller CCW` labels in the selected-key inspector so
  the physical control name remains readable in the fixed-width panel.
- Prevented Combo member edits from recursively reloading the workspace when
  programmatic Layer Scope checks raised deferred `ItemCheck` callbacks.
- Prevented empty Tap & Hold and Combo workspaces from showing a stale, apparently
  assignable palette state; unavailable cards now share an explicit next-step hint.
- Expanded the Combo Layer Scope checklist so all four default layers are visible.
- Prevented the header overflow button from clipping and kept Live/Stop/Reset
  vertically aligned with the adjacent action buttons.
- Marked the WinForms entry point STA so OLE drag/drop registration cannot abort
  startup after the action dock gained drag sources and key targets.
- Prevented the first Open/Save file picker from freezing the WinForms UI on
  systems where Explorer-style common-dialog initialization stalls.
- Action Palette alternatives now replace the selected exclusive action in one
  click instead of requiring a separate uncheck, while only unsupported roller
  actions remain unavailable.
- The action preview now identifies the selected physical control and labels the
  value as its draft, preventing static layer help from being mistaken for the
  selected binding summary.

### Known limitations

- Direct Bluetooth is not supported.
- The bundled USB receiver is not hardware-tested because it is unavailable.
- Default app configuration persistence, tray UI, draft autosave, and
  wheel-center binding are not yet implemented.

### Verified

- The public `46slv/ACK05-Enhanced` repository recognizes the MIT license and
  completed its first clean `windows-latest` build plus 40/40 self-tests.
- At 200% Windows display scaling, the maximized Release app kept Keymap,
  Tap & Hold, Combos, and Profiles reachable through the compact workspace
  selector. The physical map, inspector, Action Palette, K9 typography, Combo
  badges, and Output OFF boundary remained visible and usable.
- Windows enumerates the connected ACK05 vendor collection as `FF0A:0001` with
  32-byte input/output reports.
- Read-only battery reports and the documented raw-mode command response are
  observable through the probe.
- Synchronized Windows hardware input confirms K1 at byte 2 bit 0, K2 at byte 2
  bit 1, clockwise roller at byte 7 value `01`, and counter-clockwise roller at
  byte 7 value `02`.
- A second synchronized capture confirms K3-K8 at byte 2 bits 2-7 and K9-K10 at
  byte 3 bits 0-1, including down and zero-state release reports.
- K1-held combinations route K2-K10 and roller pulses through Layer 1 on hardware.
- Disconnecting wired USB while K2 is held produces a device-disconnected error
  and terminates the probe.
- Deterministic fake-transport tests verify disconnect reset, reconnect, shutdown
  reset, source disposal, and non-cancelable safety delivery.
- GUI Live mode reached Connected with the saved K2/K3/K4 and Layer 1 wheel
  configuration; Stop Live reported pressed-key release and reconnect succeeded.
- Deterministic tests verify Momentary, Toggle, short-tap toggle, long-hold shift,
  chord-as-hold behavior, captured-layer key-up, and reset to Layer 0.
- Deterministic tests verify a non-K1 layer control, K1 action routing, and
  validation of the dynamically reserved control.
- Deterministic tests verify `K2=A` / `K1+K2=B`, Transparent fall-through,
  highest-active-layer precedence, captured key-up, schema migration, and the
  complete GUI Live input pipeline (28/28 passing).
- Hardware `watch` verifies K2 Up reset before Disconnected status and automatic
  reconnection after the USB cable is restored.
- Hardware shutdown verifies K3 Up reset is emitted before Stopped status when
  Ctrl+C occurs while K3 is physically held.
- Hardware configured output verifies F24 in Layer 0, Shift+F24 in Layer 1,
  signed wheel output, disconnect release, reconnect, and shutdown panic release.
- Direct Bluetooth enumerates four standard HID collections but no `FF0A:0001`
  vendor collection. No device-attributable physical state report was observed.
