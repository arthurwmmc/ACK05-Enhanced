# ACK05 Enhanced v1 Specification

Status: Draft / schema v4 Open Target, Radial Menus, Tap & Hold, two-key Combo, USB input, SendInput, profiles, and editor implemented

Date: 2026-07-21

## Open Target v1 acceptance contract

- A single `Open Target` leaf action accepts one target string. The target may be
  an executable, file, folder, Windows shortcut, or absolute HTTP/HTTPS URL.
- Activating the action once delegates to the Windows shell `open` behavior:
  folders open in Explorer, URLs in the default browser, associated files in
  their registered application, and executables start normally.
- If the desired child file is known, assigning that child path replaces the
  manual "open folder, then click" sequence with one ACK05 press.
- The action fires once on a physical Down or pulse. Up, reset, reconnect, and
  configuration replacement never repeat or terminate a launched target.
- `Open Target` is available to ordinary bindings, Tap output, and Combo output.
  It is not available as a held Tap & Hold action because it has no held state.
- GUI output remains opt-in through `Output ON`. The Probe CLI requires a
  separate explicit target-launch permission in addition to its existing device
  write and SendInput permissions.
- Launch failure is reported without stopping the input runtime or disturbing
  the pressed-key safety registry.
- Schema v1-v3 configurations remain loadable. Saving writes schema v4; the new
  action payload is `{ "kind": "openTarget", "target": "..." }`.
- v1 does not interpret command strings, request elevation, wait for or terminate
  launched processes, sequence multiple targets, or persist a working directory
  and argument list.

## Implemented Radial Menu slice

- Each profile owns reusable Radial Menu definitions with a stable ID, name,
  activation input, forward/backward navigation inputs, 100-1000 ms hold
  threshold, 1-10 pulse navigation step, pointer/monitor-center placement, and
  exactly 4, 6, or 8 ordered segments.
- Holding the activation input beyond the threshold opens a focusless overlay.
  A short press replays the activation input's ordinary binding unchanged.
- The menu starts with no selection. Forward selects segment zero, backward
  selects the last segment, subsequent pulses wrap, and activation release runs
  the selected leaf action exactly once. Release without selection cancels.
- While open, activation and navigation events are consumed. Escape, Output OFF,
  disconnect, profile/config replacement, reset, and shutdown close without
  executing a segment.
- Initial segment outputs are Disabled, Keyboard Chord, Mouse Wheel, Open Target,
  and TG(n). Held outputs, nested Tap & Hold, Combos, arbitrary command strings,
  and nested radial menus are rejected.
- The continuous monochrome wheel has external labels, built-in Windows action
  icons, a compact center, and a solid selected segment. Pointer placement clamps
  the complete 520 x 520 overlay inside the active monitor working area with a
  16-pixel safety margin. Active-monitor center is available as an alternative.
- Radial Menus has a dedicated three-column editor for definitions, clockwise
  segments, and settings, plus a safe preview that never executes an action.

## Implemented Instrument Workbench UI slice

- The hardware map exposes the roller center as `Roller Press`. It is a stateful
  Down/Up control and supports the same leaf bindings, layer actions, Tap & Hold,
  and Combo membership as K1-K10. Only the two rotation directions are pulse-only.

- The main Keymap workspace uses a hardware-first map, a persistent right-side
  inspector for the selected control, and a bottom action library.
- Selecting a valid action card or changing its relevant inspector parameter
  updates the in-memory configuration immediately; `Apply current draft` remains
  collapsed under Advanced as a recovery affordance.
- The action library supports category filtering, text search, multi-key keyboard
  chords, and dragging the currently selected action onto K1-K10.
- The editor and bottom action library are separated by a horizontal splitter.
  The library starts tall enough to show multiple card rows, grows with additional
  window height, and can be resized vertically without hiding the physical map.
- Empty Tap & Hold and Combo selections disable the shared action library and show
  a direct next-step hint instead of retaining a previous workspace's active state.
- Every committed binding, layer, or profile edit enters session Undo/Redo.
  Explicit Save updates the saved baseline without clearing history. Revert to
  Saved returns to that baseline and can itself be undone.
- Valid edits hot-swap the active output runtime without writing the JSON file. Save is the
  only normal file persistence action.
- Dirty working drafts are debounced into a separate Local App Data recovery file.
  Startup offers Restore or Discard; recovery retains the explicit-Save baseline
  so Revert remains meaningful after restart. A clean baseline removes the draft.
- The recovery envelope records the source path, saved-file fingerprint, and UTC
  timestamp. A changed source file blocks silent overwrite and offers Overwrite,
  Save As, or Cancel.
- Keymap, Tap & Hold, Combos, Radial Menus, and Profiles are separate workspaces.
  Tap & Hold, Combos, and Radial Menus manage profile-owned definitions through
  the shared schema-v4 action library.
- Tap & Hold keeps a compact current-layer ACK05 map visible between its definition
  list and inspector. The selected definition highlights its current-layer
  assignments, exact `Lx Ky` references are shown, and K1-K10 can be selected as
  the assignment target without returning to Keymap.
- Known Tap & Hold bindings resolve to their compact Tap/Hold result on device maps
  instead of exposing only the internal definition ID.
- The Keymap draws compact monochrome badges for Combos eligible on the selected
  layer. A badge shows its output and layer scope, highlights both member keys on
  hover/focus, and opens the definition in the Combos workspace when activated.
- A compact Keymap view toggle shows the current-layer Combo count while badges
  are visible and changes to `Combos OFF` when hidden. This is process-local view
  state: it does not edit configuration, enter Undo history, affect Save, or
  disable Combo routing.
- The output boundary is presented as the stateful `Output OFF` / `Output ON`
  control. The editor always opens with output OFF; connection/waiting/error
  details remain separate in the status bar. The former Live runtime semantics,
  explicit first-use warning, hot-swap, and panic release are unchanged.
- Workspace navigation shows the five direct pills while they fit. When their
  container becomes narrower than their measured widths, all five switch atomically
  to one active-workspace menu containing Keymap, Tap & Hold, Combos, Radial Menus,
  and Profiles.
  The compact menu is separate from the `•••` file menu and is not persisted.
- Opening Keymap, Tap & Hold, Combos, or Radial Menus resets that workspace's inspector to its
  first field so a previous scroll position cannot hide the current context.
- Owner-drawn navigation, physical keys, action cards, and Combo badges preserve
  native keyboard traversal and draw a monochrome focus cue. The roller is a
  keyboard target; Left/Right Arrow selects CCW/CW for editing.
- UI motion uses short ease-out transitions for hover, selection, and the
  Advanced inspector drawer. Motion never delays input routing or output release.
- Palette and map state updates reuse existing controls when only selection or
  capability state changed; workspace navigation must not rebuild unchanged card
  collections or restart unchanged key animations.

## Implemented reusable Tap & Hold slice

- Each profile owns reusable definitions with stable IDs, display names, Tap and
  Hold leaf actions, a 100-1000 ms tapping term, and one of three interrupt modes.
- Tap actions: Disabled, Keyboard Chord, Mouse Wheel, Open Target, and TG(n).
- Initial Hold actions: Disabled, held Keyboard Chord, and MO(n).
- A key binding references a definition by ID. Roller directions cannot use Tap & Hold.
- `Hold on other key press` resolves Hold before routing the interrupting key;
  `Hold on other key tap` waits for its release; `Timeout only` ignores interrupts.
- A 10 ms host timer resolves elapsed holds even when no new physical event arrives.
- Pending taps are discarded on reset. Started Hold output and MO layers always
  release on key-up, disconnect, Output OFF, profile/config replacement, and shutdown.
- The Tap & Hold workspace creates, edits, deletes, and assigns definitions while
  reusing the shared action library. Referenced definitions cannot be deleted.
- Schema v1-v3 files still load; the in-memory model upgrades to schema v4
  without converting legacy `tapToggleHoldLayer` actions.

## Implemented two-key Combo slice

- Each profile owns exactly-two-key Combo definitions. Roller directions and
  duplicate members are rejected.
- A Combo has a layer scope, profile-default 20-200 ms recognition term, optional
  per-definition term, optional displayed order, trigger mode, and release policy.
- Trigger modes are On second press, Tap only, and Hold together. The latter two
  expose a 50-2000 ms Hold Term.
- Outputs may be Disabled, Keyboard Chord, Mouse Wheel, Open Target, MO(n), or TG(n).
- Candidate member events are buffered before normal bindings and Tap & Hold.
  A match suppresses both member bindings; a failed attempt replays each event
  with its captured timestamp into the normal resolver.
- The first member snapshots the active layer. Only definitions in that layer
  scope participate, so a buffered member cannot change its own Combo scope.
- Press matches are confirmed on the 10 ms host timer. This leaves one input
  batch for a third key to fail closed instead of choosing an arbitrary pair.
- Reset discards pending candidates and releases any held Combo keyboard or MO output.
- The Combos workspace creates/deletes definitions, chooses both keys directly
  on the ACK05 map, edits all timing parameters, and chooses output from the shared palette.

## 目的

ACK05の物理キー状態を直接読み、Windows上でVial / QMK型のレイヤー付き
キーマップを構成する。レイヤーを先に作り、あとから任意のK1-K10へ
レイヤー操作を割り当てられることを中心体験とする。

## 対象

- Windows 10/11 x64、.NET 8 Desktop
- ACK05を制作作業用の左手デバイスとして使うユーザー
- 現行transport: USB-C有線、付属USB receiver
- Direct Bluetoothは、Windowsでphysical identityを取得できるtransportが未確立

## 実装済み機能

1. VID/PIDとvendor usageでACK05を識別する。
2. K1-K10のdown/upとroller左右pulseを取得する。
3. profileごとに2-8レイヤーを持ち、新規設定は4レイヤーとする。
4. K1-K10とroller左右を、全レイヤーで編集可能にする。
5. `disabled`、`transparent`、keyboard chord、mouse wheelを割り当てる。
6. 任意キーへ `MO(n)`、`TG(n)`、Tap Toggle / Hold Layerを割り当てる。
7. 複数レイヤーがactiveなら高い番号を優先し、Transparentは下へ透過する。
8. foreground processに応じてprofileを切り替える。
9. disconnect、profile/config変更、終了時に保持出力を必ずreleaseする。
10. GUIからOpen、Save、Save As、Validate、Output OFF/ONを操作する。

## 操作フロー

1. アプリ起動時はOutput OFFとする。
2. profileを選び、必要なら `Layers > Add Layer` で最大8枚まで追加する。
3. レイヤータブと物理キーを選び、通常actionまたはlayer actionを設定する。
4. 例: Layer 0のK2へA、K1へMO(1)、Layer 1のK2へBを設定する。
5. K2単独はA、K1保持中のK2はBを出力する。
6. SaveはJSONを永続化する。Output ONは明示許可後にraw modeとSendInputを開始する。

## レイヤー意味論

- Layer 0は常にactive。
- `MO(n)`: key downからupまでLayer nをactiveにする。
- `TG(n)`: key downごとにLayer nをtoggleする。roller pulseでも使用可能。
- Tap Toggle / Hold Layer: down直後からLayer nをactiveにし、threshold未満の
  単独tapだけtoggleする。長押しまたは別controlとの組み合わせはmomentaryになる。
- active layerが複数なら番号が最も高いレイヤーを先に解決する。
- `transparent` は次のactiveな下位レイヤーへfall-throughする。
- 通常actionはdown時に解決したlayerを記録し、途中でactive layerが変わっても
  同じlayerへupをroutingする。
- resetは全momentary/toggle状態を消し、Layer 0へ戻す。

## UI構造

- 上: Keymap / Tap & Hold / Combos / Profiles、Output OFF/ON、Undo/Redo、Revert、Save
- context bar: profile selector、2-8枚のlayer selector、追加／削除
- 中央左: 実機配置に沿うroller、K1-K10 map
- 中央右: 選択キー専用inspector。action summaryと現在actionに必要なparameterだけを表示
- 下: 検索、category、click/D&D対応のAction Libraryとstatus

Action LibraryはCommon、Layers、Navigation、Normal Keys、Modifiers、Function Keys、
Numpad、Media、Other Keysへ分類する。DisabledとTransparentはkeyboardと同じlibraryから
直接選択できる。keyboard keyは複数選択できるが、Disabled、Transparent、Mouse Wheel、
MO、TG、Tap Toggle / Hold Layerは単独actionとして扱い、別のvalid actionを押すと一操作で
置き換える。無効表示はroller上のMOなど、context上実行できない候補だけに使う。

K1-K3 / K4-K6、縦長K7、K8 / 横長K9 / K10と大型rollerの配置は
ACK05の実機参照図に合わせる。選択中のキーとbinding rowは同期する。

## 保存形式と互換性

正式contractは [CONFIG_FORMAT.md](CONFIG_FORMAT.md) を参照する。schema v2は
profile、2-8 layers、全control binding、logical actionを持つ。schema v1はload時に
自動移行し、旧global layer switchをLayer 0上のMO/TG/Tap-Hold actionへ変換する。
保存時はschema v2となる。未知fieldは可能な範囲で保持する。

## エラー処理と安全

- Device absent: 再接続を監視し、未接続状態を表示する。
- Raw-mode command失敗: synthetic outputを開始しない。
- Malformed report: 無視して診断可能な形で扱う。
- Invalid config: 保存・Output ON/即時反映を拒否し、最初の具体的errorを表示する。
- Layer削除: 参照中のtarget layerなら削除を拒否する。
- Disconnect / profile / config / shutdown: Core stateとWindows registryの両方をpanic releaseする。
- Driver、HidHide、firmware、pairingは変更しない。

## Undo方針

binding、layer、profile編集は起動中のUndo/Redo historyへ入る。Save後もhistoryを保持し、
最後の明示Saveをbaselineとする。Revert to Saved自体もUndo可能。Open、New、process終了では
historyを破棄する。working draftはLocal App Dataへdebounce保存し、再起動時にRestore / Discardを
提示する。保存済みファイルには `.bak` を残す。
OS出力には一般的なUndoがないため、保持状態を確実にreleaseすることを優先する。

## v1でやらないこと

- Firmware書き換え、driver/HidHide自動設定、pairing変更
- Direct Bluetooth protocol完成保証
- `TO`、`OSL`、`LT`、`LM`、Vial互換の複数tap `TT`
- 任意script、MIDI、HTTP、OBS連携
- tray常駐、自動起動

## 将来拡張

- Action LibraryのRecent/Favorites
- Tap側とHold側へ独立actionを設定する再利用可能なTap Dance定義。
  要件は [docs/design/TAP_DANCE_REQUIREMENTS.md](docs/design/TAP_DANCE_REQUIREMENTS.md) を参照する。
- `TO` / `OSL` / `LT` / `LM` / true `TT`
- layer名、copy/paste、import/export
- default per-user config、tray、live reload

## 既知リスク

- Direct Bluetoothではdevice識別付きphysical stateを取得できていない。
- XPPen純正driverと二重入力になる可能性がある。
- SendInputは高いintegrity levelのアプリへ届かない場合がある。
- output failureや強制終了時にkey-upを失う危険があるため、二重registryとpanic releaseを維持する。
