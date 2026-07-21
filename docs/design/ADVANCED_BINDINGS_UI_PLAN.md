# Advanced Bindings and UI Plan

Status: Phase C Tap & Hold and Phase D initial two-key Combo implemented

Date: 2026-07-17

## Purpose

ACK05 Enhanced should keep the physical controller easy to understand while adding
three advanced capabilities:

1. one-click action replacement;
2. independent Tap and Hold actions;
3. visual Combos such as `K1 + K2 -> Enter`.

The target is a Vial-like editing posture, not Vial protocol compatibility. Layers,
reusable behavior definitions, and combos are created first and assigned afterward.
The first-time user should still be able to select one physical key and assign a
normal key without understanding behavior terminology.

## Source references

- User-supplied Vial Keymap screenshot, 2026-07-17. The important structure is a
  persistent physical keymap above an action library, with Tap Dance and Combos as
  separate workspaces.
- User-supplied keymap-drawer-style combo screenshot, 2026-07-17. The important
  structure is an output badge visually anchored between its input keys.
- Vial user manual: <https://get.vial.today/manual/>
- Vial Tap Dance: <https://get.vial.today/manual/tap-dance.html>
- Vial Combos: <https://get.vial.today/manual/combos.html>
- QMK Combos: <https://docs.qmk.fm/features/combo>
- ZMK Hold-Tap: <https://zmk.dev/docs/keymaps/behaviors/hold-tap>
- ZMK Combos: <https://zmk.dev/docs/keymaps/combos>
- ZMK Keymap Editor spotlight: <https://zmk.dev/blog/2023/11/09/keymap-editor>
- Keymap Editor features: <https://github.com/nickcoutsos/keymap-editor/wiki/Features>

## Product principles

### One selected key, one next action

Selecting a valid action should replace the selected binding in one click. A user
must not clear Disabled, Transparent, TG, MO, or another single action before
choosing the replacement.

The newer one-click rule supersedes the previous rule that gray-disabled every
other action while a single action was selected:

- mutually exclusive but valid alternatives remain clickable;
- clicking an alternative automatically clears the previous selection;
- only truly unsupported choices are gray, such as MO on a roller pulse;
- keyboard keys remain multi-selectable while constructing a chord;
- clicking a keyboard key while Disabled or another single action is selected
  immediately begins a keyboard chord with that key.

The final target removes `Apply Binding`: an action click updates the in-memory
draft immediately, while `Save` remains the only file write. Before removing Apply,
the editor needs Undo/Redo, a dirty indicator, and a clear saved/draft distinction.
An interim implementation may keep Apply while adopting automatic replacement.

When Live is running, every valid draft mutation also replaces the active runtime
configuration immediately. This is a Live preview, not a file save. The replacement
must cancel pending Combo/Tap & Hold decisions, release all held output, replace the
router atomically, and then resume input. `Revert to Saved` restores the last explicit
Save snapshot to both the draft and Live runtime without writing a file.

### Progressive disclosure

The normal-key path stays compact. Timing, interrupt, layer scope, release policy,
and reference counts appear only when the selected behavior needs them. Advanced
settings start collapsed.

### Reuse one action chooser

The same action chooser is used for a normal key binding, Tap, Hold, and Combo
output. Context capability rules decide which actions are enabled; separate copies
of the keycode lists must not drift.

## Proposed information architecture

Use workspace tabs similar to Vial:

1. `Keymap`
2. `Tap & Hold`
3. `Combos`
4. `Profiles / Settings`

Global profile, connection, Live, dirty state, Undo/Redo, and Save remain outside
the workspace tabs.

### Keymap workspace

- Keep the ACK05-shaped physical map as the primary selector.
- Show the current layer next to the map and keep layer add/remove close to it.
- Place the action library below the map in a stacked layout and beside the map in
  a wide layout.
- In the stacked desktop layout, give the action library extra window height and
  expose a horizontal divider so the user can expand or contract the library
  without changing workspace or opening another dialog.
- Action categories should initially be `Basic`, `Modifiers`, `Function`,
  `Navigation`, `Media`, `Layers`, `Tap & Hold`, and `User`.
- A selected key shows a compact summary such as `B / Hold Shift`,
  `B / Hold MO(1)`, or `Combo member: K1+K2 -> Enter`.
- Search/filter is part of the first reconstructed palette, following Keymap
  Editor's searchable behavior/keycode approach.
- Empty reusable-action and Combo selections keep the library visible for spatial
  consistency, but gray its actions and explain what must be created or selected.
- Keep the physical map visible while the library scrolls. Add `Recent` and
  user-pinned `Favorites` above the normal categories so common assignments do not
  require long-distance dragging.

### Drag-and-drop assignment

Every assignable item in the action library can be dragged onto a physical key.
This is an additional fast path, not the only path; click-to-assign and keyboard
operation remain fully supported.

- Dragging starts only after the normal pointer movement threshold so a click does
  not accidentally become a drag.
- The drag ghost shows the action summary, such as `A`, `Ctrl+C`, `MO(1)`, or
  `TH0: B / Shift`.
- Valid physical keys highlight before drop. Invalid targets show a forbidden
  cursor plus a short reason, such as `MO needs key down/up; roller is pulse-only`.
- Dropping replaces the target binding in the in-memory draft immediately. It does
  not write the file or require Apply Binding.
- One drop creates one Undo entry, including the complete previous binding.
- A leaf keyboard key dropped onto a key assigns a one-key chord. Reusable chords,
  Tap & Hold definitions, and user actions can be dragged as complete actions.
- Building or extending an arbitrary multi-key chord remains an explicit palette
  or Capture operation; repeated drops do not silently accumulate keys.
- Dragging a physical key onto another physical key to copy its binding is deferred
  until the basic library-to-key path is stable.
- The action library auto-scrolls during drag and keeps category/search state after
  the drop.
- Future Combo editing may accept an action drop onto a Combo badge, but that is not
  required for the first D&D slice.

### Tap & Hold workspace

- Manage reusable definitions separately from key assignment.
- List definitions on the left and edit the selected definition on the right.
- The initial editor shows only `On Tap`, `On Hold`, `Tapping Term`, and
  `Interrupt Mode`.
- `Double Tap`, `Tap + Hold`, quick tap, positional rules, and other advanced
  fields remain collapsed or deferred.
- `Create and Assign` is available when this workspace was opened from a selected
  physical key.
- Editing a shared definition shows its reference count and affected keys/layers.

### Combos workspace

- Show a combo list and the ACK05 physical map together.
- Creating a combo enters an input-selection mode; clicking K1 then K2 produces
  `K1 + K2` without typing position numbers.
- The output uses the shared action chooser.
- Always-visible fields are Name, Input Keys, Output, Recognition Window
  (`Combo Term`), and Layer Scope.
- Roller directions are excluded because they are pulses rather than held keys.
- Initial combos contain exactly two keys. Three-key and larger Combo definitions
  are rejected. The default Combo Term is 50 ms, configurable from 20-200 ms.
- Profile Settings contains a default Combo Term. Each Combo starts with
  `Use profile default` and can enable an individual override.
- A collapsed `Detection Details` section contains:
  - `Key Order`: Any order / In displayed order;
  - `Trigger`: On second press / Tap only / Hold together;
  - `Hold Term`: shown only for Tap only or Hold together;
  - `Release`: First member released / All members released.
- `Tap only` requires both members to be released within Hold Term. If the chord is
  held past that term, it fails and the original member events are replayed.
- `Hold together` requires both members to remain down for Hold Term. Releasing a
  member early fails and replays the original events.
- The fixed fail-closed rule for simultaneous competing pairs and the exactly-two-
  key limit are safety semantics, not user-adjustable controls.
- Timing fields use numeric input rather than a free-running slider. One spinner
  step, Enter, or focus-commit creates one Undo transaction and one Live hot-swap;
  incomplete or invalid text remains local to the editor.
- While Live is running, a read-only test line shows the last attempt, for example
  `K1 → K2 · 34 ms · Matched` or `67 ms · Too slow`. This is diagnostic feedback,
  not another save/apply action.

## Combo visualization on the Keymap

The physical Keymap workspace should display combos, not only the Combos tab.

- Draw a compact blue output badge at the visual center of the member keys.
- Example: a badge labeled `Enter` between K1 and K2 represents
  `K1 + K2 -> Enter`.
- Hovering or selecting the badge highlights all member keys and draws connector
  lines; connectors may remain hidden otherwise to reduce noise.
- Clicking the badge opens that Combo in the Combos workspace.
- A layer-limited combo includes a small `L0`, `L1`, or `L0-2` scope label.
- Show only Combos eligible on the current layer by default. `All Layers` is an
  explicit alternate view.
- Overlapping combos stack their badges. Only the selected combo expands its
  connectors and full name.
- Nearby pairs use the centered badge. For distant pairs such as K1+K7, show a
  badge in a compact Combo rail next to the map plus matching endpoint chips on
  both keys; draw the long connector only on hover or selection.
- A global `Show Combos` toggle allows an uncluttered keymap view.
- The combo list mirrors every overlay so the feature remains operable without
  relying on color or precise pointer targeting.

## Hold and Tap behavior model

The editor follows Vial's Tap Dance posture: a reusable definition can eventually
contain `On Tap`, `On Hold`, `On Double Tap`, and `On Tap + Hold`, plus a tapping
term. The first implementation exposes only the first two slots and calls the
workspace Tap & Hold; the other two slots extend the same definition later.

```text
Tap:       B
Hold:      Left Shift
Term:      200 ms
Interrupt: Hold on other key press
```

Required examples:

1. `Tap=B / Hold=Left Shift`
   - a short solo press emits B;
   - holding past the term presses Shift until the physical key is released;
   - with hold-on-other-key-press, Shift must be pressed before the interrupting
     key is routed.
2. `Tap=B / Hold=MO(1)`
   - a short solo press emits B;
   - holding activates Layer 1 only while the physical key remains down;
   - an interrupting key is routed after Layer 1 becomes active.

Initial slot capabilities:

| Slot | Allowed actions |
|---|---|
| Tap | Disabled, Keyboard Chord, Mouse Wheel, TG(n) |
| Hold | Disabled, held Keyboard Chord, MO(n) |

Nested Tap & Hold, Combo, Macro, or another composite action is not allowed in the
first schema-v3 slice. This avoids recursive release ownership and keeps validation
and panic release auditable.

### Action capability model

Combo timing and action capability are separate concerns. Combo timing decides
whether two physical controls form a chord. Capability metadata decides where an
action can be used and how it must be released. One Core registry is shared by the
Keymap, Tap, Hold, Combo, and D&D UIs. It records whether an action:

- is a pulse or has a Down/Up lifetime;
- can own held keyboard output;
- can be used in Tap, Hold, Combo output, or on a roller pulse;
- requires a target layer;
- may contain or reference another action.

This registry drives gray states and validation so the UI cannot offer an action
that the runtime cannot safely execute.

## Combo runtime semantics

Initial Combo output may be Disabled, Keyboard Chord, Mouse Wheel, MO(n), or TG(n).
Composite Tap & Hold output is deferred.

The event order must be explicit because Combo and Tap & Hold can share keys:

1. physical key events enter Combo detection first. A strict timer starts at the
   first member Down; both members must be Down within the Combo Term;
2. the active profile and active-layer stack are snapshotted at the first member
   Down, and only Combos eligible in that snapshot participate;
3. candidate member-key events are buffered until a combo succeeds or its term
   expires. A member's own MO/Tap & Hold action cannot change the snapshot while
   it is buffered; an MO already held before the first member does count;
4. a successful combo suppresses the individual member bindings and starts the
   combo output;
5. a failed candidate replays the original events and timestamps into the normal
   binding/Tap & Hold resolver;
6. after Combo failure, a Vial/QMK-style interrupted Tap & Hold resolves before
   routing the interrupting key;
7. no physical down or up may be emitted twice;
8. disconnect, Stop Live, profile/config change, exception, and shutdown cancel
   pending combos and release any started combo output.

Different two-key Combos may share one member, such as K1+K2 and K1+K3. The first
pair completed normally wins. If one physical report/timestamp would complete more
than one pair because three or more keys arrived together, no Combo fires; the
buffer is replayed as ordinary input. This fail-closed rule avoids enumeration-order
dependent output while three-key Combos are outside scope.

A key may be both a Combo member and Tap & Hold key, but the UI must warn that the
Combo Term can delay its Tap/Hold decision. This mirrors the timing interaction
documented by ZMK rather than hiding it.

Initial release behavior ends a held Combo output when any member key is released.
Selecting `All members released` keeps the output held until both members are up.
The owner-token registry still prevents either policy from releasing output owned
by another binding.

Every started output also carries an owner token. If Tap & Hold, a normal binding,
and a Combo all hold Left Shift, releasing one owner decrements ownership but does
not emit Shift Up until the final owner releases it. Cancellation removes all owner
tokens and performs the final matching releases.

## Data model direction

Schema v3 should separate leaf actions from reusable composite definitions:

```json
{
  "comboDefaults": { "termMs": 50 },
  "tapHolds": [
    {
      "id": "th0",
      "name": "Shift / B",
      "tappingTermMs": 200,
      "interruptMode": "holdOnOtherKeyPress",
      "onTap": { "kind": "keyboardChord", "keys": ["b"] },
      "onHold": { "kind": "keyboardChord", "keys": ["leftShift"] }
    }
  ],
  "combos": [
    {
      "id": "combo0",
      "name": "K1 + K2 Enter",
      "controls": ["k1", "k2"],
      "layers": [0],
      "termMs": null,
      "ordered": false,
      "trigger": "press",
      "holdTermMs": null,
      "release": "firstMember",
      "action": { "kind": "keyboardChord", "keys": ["enter"] }
    }
  ]
}
```

A null or omitted `termMs` uses the profile default. `holdTermMs` is required only
for `tapOnly` and `hold` trigger modes; irrelevant fields serialize as null or are
omitted. The loader normalizes both forms to the same model.

Bindings reference a Tap & Hold definition by stable ID. Combo definitions are
profile-owned and reference physical controls plus an action. Schema v2 and legacy
`tapToggleHoldLayer` must continue to load unchanged; no silent conversion occurs.

## Error, Undo, and compatibility policy

- An invalid or missing definition blocks Save and Live apply with a specific path.
- Referenced definitions cannot be deleted without showing every reference.
- Direct action assignment enters the Undo history before mutating the draft.
- Undo/Redo applies to key assignments, definition edits, combo edits, layer edits,
  and profile edits.
- Save updates the saved baseline but keeps the current session history. Undo may
  cross a Save boundary and makes the draft dirty again; Redo may return to it.
- `Revert to Saved` is one Undoable transaction. Immediately after Revert the
  draft is clean; undoing that Revert restores the previous working draft and marks
  it dirty again.
- A new mutation after Undo clears the abandoned Redo branch, matching common
  desktop-editor behavior.
- Open, New, or switching to another configuration file clears history. Closing
  the application also clears history.
- Layer deletion and every affected reference are one transaction, so one Undo
  restores the layer, bindings, Tap & Hold references, and Combo scopes together.
- Referenced layers are not silently deleted. The first implementation blocks the
  deletion and lists references; explicit remapping can be added later.
- Save remains atomic and preserves `.bak`.
- The last explicit Save and the working draft are persisted separately. Recovery
  drafts live under the per-user Local App Data directory, not beside the user's
  JSON. They include the source path, saved-file fingerprint, and timestamp.
- After an abnormal exit, startup offers `Restore Draft` or `Discard`. A clean
  close with no dirty changes removes the recovery draft. Recovery preserves the
  latest draft snapshot, not the complete Undo/Redo history.
- Before overwriting an existing file, compare its fingerprint with the one loaded
  or last saved. An external change blocks silent overwrite and offers Reload,
  Save As, or explicit Overwrite.
- Existing schema-v2 files remain readable and retain their current semantics.
- Held actions always receive matching release during cancellation and shutdown.

## Implementation sequence

### Phase A: Interaction polish without schema change

- Automatic replacement when another valid action is clicked.
- Gray only unsupported actions, not alternative single actions.
- Drag basic, layer, and reusable actions from the library onto physical keys.
- Add palette search/filter.
- Add dirty state and Undo/Redo foundation.
- Remove Apply Binding only after Undo/Redo is reliable.

### Phase B: Workspace reconstruction without behavior changes

- Keymap / Tap & Hold / Combos / Profiles tabs.
- Responsive map plus shared action library.
- Empty Tap & Hold and Combos states with explanatory copy.
- Combo overlay renderer using mock/in-memory definitions.

### Phase C: Tap & Hold schema and runtime

- Schema-v3 reusable definitions and schema-v2 compatibility.
- `Tap=B / Hold=Shift` first.
- `Tap=B / Hold=MO(1)` second.
- Interrupt modes, deterministic tests, and hardware timing tests.

### Phase D: Combos

- Combo model, resolver, cancellation safety, and validation.
- `K1 + K2 -> Enter` first.
- Layer scope, overlapping combos, overlays, and editor.
- Tap & Hold membership timing warnings.

### Phase E: GitHub readiness

The repository currently has no remote, LICENSE, SECURITY file, or GitHub workflow.
Before public upload:

- decide owner/repository name and public versus private visibility;
- choose a license explicitly;
- exclude personal `ack05-config.json` and backups while keeping safe examples;
- add a Windows/.NET build-and-self-test GitHub Actions workflow;
- add release packaging, screenshots, known limitations, and a release checklist;
- audit documentation for private paths, captures, and device-specific data;
- create the remote and push only after the user confirms the destination and
  visibility.

### Future: Radial action menu

After the Keymap, Tap & Hold, and Combo foundations are stable, add a reusable
hold-to-open radial menu controlled by the ACK05 roller. Releasing the trigger key
executes the highlighted action. The preserved concept and safety boundary are in
`docs/design/RADIAL_ACTION_MENU_CONCEPT.md`.

## Acceptance criteria for the design slice

1. From Disabled, one click on A prepares or assigns A without clearing Disabled.
2. From TG(1), one click on B replaces TG with Keyboard B.
3. Only context-invalid actions are gray.
4. Dragging A, MO(1), or a reusable Tap & Hold definition onto a valid key replaces
   its draft binding in one Undoable operation.
5. Invalid drop targets explain why the action cannot be assigned, and every D&D
   operation has an equivalent click/keyboard path.
6. `Tap=B / Hold=Shift` and `Tap=B / Hold=MO(1)` are understandable from the keymap
   summary without opening the definition editor.
7. `K1 + K2 -> Enter` is visible as a badge between K1 and K2 and editable by
   clicking that badge.
8. Overlays remain readable with multiple and overlapping combos.
9. Combo and Tap & Hold timing conflicts are warned about and deterministic.
10. Save, Live apply, disconnect, and shutdown never leave a held output active.
11. Live reflects every valid draft change without Save, and Revert to Saved returns
    both the draft and Live behavior to the last explicit Save.
12. Action summaries identify the physical control and draft state; static help
    text cannot be mistaken for the selected binding.
13. Palette search, Recent, Favorites, and a non-scrolling physical map keep common
    assignments reachable without a long drag.
14. Combo Term, key order, trigger mode, conditional Hold Term, and release policy
    are editable without JSON editing; advanced fields remain collapsed by default.
15. A committed parameter edit is one Undo entry and one Live update, and the Live
    diagnostic reports the measured inter-key time and match result.
