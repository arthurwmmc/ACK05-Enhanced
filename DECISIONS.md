# Decisions

## 2026-07-21: Publish ACK05 Enhanced as a public MIT-licensed project

### Background

The editor and deterministic runtime tests are mature enough to prepare for public
source hosting. A clear reuse license, public-facing README, and source-hygiene
boundary are required before creating the GitHub remote.

### Options

1. Keep the repository private until binary packaging and signing are complete.
2. Publish the source publicly without an explicit license.
3. Publish the source publicly under the MIT License while accurately labeling it
   as an unsigned source-build preview.

### Decision

Use option 3. The repository name is `ACK05-Enhanced`, visibility is public, and
the source license is MIT. The README must state the unsupported transport and
packaging boundaries. A Japanese note article follows the first preview release;
it is not a substitute for repository documentation.

### Reason

MIT is a familiar permissive license for a small Windows companion tool and makes
reuse rights explicit. Public source can receive testing and contributions before
the packaging/signing work is finished, provided current limitations are visible.

### Impact

Public commits must exclude personal configs, captures, device paths, secrets, and
generated output. The repository continues to make device writes and SendInput
explicit opt-ins. Public status does not imply XP-Pen affiliation or official
direct Bluetooth/receiver support.

### Rollback

Future releases may change license only for new contributions with contributor
agreement. Already published MIT-licensed revisions remain available under MIT.

## 2026-07-21: Separate editor lifetime from explicit output state

### Background

The editor is usable for the whole process lifetime, but the `Live` / `Stop` /
`Reset` labels mixed a state noun with action verbs. It was unclear whether
`Live` meant output was already active or clicking would activate it. Starting
output automatically with the editor would remove that ambiguity but would also
send the ACK05 raw-mode command and enable synthetic keyboard/mouse output without
a fresh user gesture.

Current-layer Combo badges are useful context but can obscure a dense physical
map. Removing them globally would discard the direct path into Combo definitions.

### Options

1. Start device capture and SendInput automatically whenever the editor opens.
2. Retain `Live` / `Stop` and explain the terms only in documentation.
3. Keep explicit opt-in, present one stateful `Output OFF` / `Output ON` control,
   and report waiting/connected/error details separately.

For Combo badges, either always show them, remove them, or add presentation-only
visibility state.

### Decision

Use option 3. The editor always starts with `Output OFF`; the first activation in
each process still shows the raw-mode and SendInput warning. The same button reads
`Output ON` while waiting or connected and `Output ERR` on runtime failure. The
status bar communicates device connection separately.

Add a Keymap-only `Combos <count>` / `Combos OFF` toggle. It defaults to visible,
lasts for the current process, and never changes schema data, Undo/Redo, Save,
Live/runtime configuration, or Combo recognition.

### Reason

The editor can be continuously active without implying that device capture and
synthetic output are active. Explicit opt-in protects recovery startup, unexpected
profiles, and users without a backup keyboard. Consistent state labels remove the
command/state ambiguity. Presentation-only Combo visibility reduces clutter
without making a working Combo appear disabled.

### Impact

No configuration migration or output-semantic change. Existing files remain
compatible. Screen readers receive explicit shown/hidden counts and output state.

### Revert

Restore the prior header labels and always call `LoadMap(..., showCombos: true)`.
No saved data requires reversal.

## 2026-07-16: Live output is an explicit GUI lifecycle

### Background

Saving a keymap successfully persisted JSON but did not make ACK05 controls emit
the configured output because the first editor intentionally had no device or
SendInput lifetime. The user expected the editor to make the keymap usable.

### Options

1. Make Save silently start device raw mode and SendInput.
2. Keep requiring the separate Probe CLI `run` command.
3. Add explicit Start Live / Stop Live controls backed by a reusable runtime.

### Decision

Use option 3. Live mode starts disabled. The first Start Live click displays the
raw-mode and SendInput boundary, then uses the in-memory valid configuration.
Valid edits update the active runtime after panic-releasing held output. Stop,
disconnect, profile change, and window close release held synthetic keys.

### Reason

The UI becomes operational without making Save a hidden device-write action.
The explicit control preserves the opt-in safety rule and the reusable runtime
keeps HID and SendInput details outside the form and outside Core.

### Impact

`Ack05Enhanced.App` now references the Windows adapter project. Windows HID
discovery and the report-source factory moved out of Probe so the CLI and GUI
share one transport implementation. Direct Bluetooth without the vendor
collection remains unsupported by Live mode.

### Revert

Remove the Live controls and Windows project reference from the App. The JSON
editor and Probe CLI remain independently usable.

## 2026-07-16: use the legacy Win32 file dialog for editor open/save

### Background

On the test machine, the first Explorer-style `SaveFileDialog` call stalled
before a dialog window appeared. The editor UI thread stopped pumping messages
and Windows marked the window as unresponsive.

### Options

1. Keep the Explorer-style dialog and accept the machine-specific stall.
2. Move the dialog to an unmanaged background thread.
3. Use the built-in legacy Win32 dialog with a deterministic initial directory.

### Decision

Use option 3 for both Open and Save As. Set `AutoUpgradeEnabled` to `false`, use
Documents as the initial directory when no file is open, and restore the process
directory after the dialog closes.

### Reason

This keeps explicit file-oriented open/save semantics and requires no new
dependency, helper process, default persistence path, or schema change. It also
avoids the Explorer common-dialog initialization path that reproduced the hang.

### Impact

The picker uses the older Windows appearance. Atomic JSON writing and backup
behavior are unchanged.

### Revert

Remove `AutoUpgradeEnabled = false` from the dialog factories if a later Windows
runtime fixes the Explorer-style initialization issue on the supported systems.

## 2026-07-16: 独立プロジェクトへ分離

### 背景

調査taskが誤ってroBa repositoryをcwdにして開始された。

### 決定

`ACK05-Enhanced` を独立Git repositoryにする。

### 理由

ZMK firmware設定とWindows ACK05 companionは依存関係、release、riskが異なる。

### 戻し方

新規folderをarchiveし、roBa側は変更しない。

## 2026-07-16: Firmware改造ではなくuserspace HID companionを採用

### 選択肢

1. ACK05 firmware reverse engineering
2. 既定keyboard reportをRaw Inputで変換
3. Vendor HIDのbitmask modeをuserspaceで読む

### 決定

3を採用する。

### 理由

公開実装とLinux kernel sourceに、10キー同時押し状態を保持するraw reportがある。
Firmwareを書き換えずmomentary layerに必要なdown/upを取得できる。

### 影響

Windowsでvendor collectionを開けるか、純正driverと競合するかの実機確認が必要。

### 戻し方

transport interfaceを維持したままRaw Input方式または別HID libraryへ交換する。

## 2026-07-16: v1 transportをUSBと付属receiverに限定

### 背景

公式manualは直接Bluetooth対応を示すが、既存userspace driverはBluetooth raw modeを
未対応としている。

### 決定

v1は有線USBと付属receiver。直接Bluetoothはcapture後に追加する。

### 理由

推測したcommandを送らず、最重要のhold layerを早く安全に成立させるため。

### 戻し方

Bluetooth transportを追加し、Coreは変更しない。

## 2026-07-16: 初期spikeは外部runtime dependencyなし

### 背景

HIDAPI/HidSharpは実装量を減らせるが、新規dependencyとlicense管理が増える。

### 決定

.NET 8とWindows SetupAPI/HID APIのP/Invokeだけでprobeを作る。

### 影響

native interop codeは増える。実機結果が悪ければHIDAPI採用を再評価する。

### 戻し方

`IHidTransport`相当の境界を維持し、adapterだけ置換する。

## 2026-07-16: 直接Bluetoothは列挙確認後もv1対象外を維持

### 背景

所有機をWindowsへ直接Bluetooth接続できたため、USB raw modeと同等のphysical
state取得が可能か実機確認した。

### 選択肢

1. 標準keyboard/mouse eventをdevice識別なしで入力として採用する
2. 推測したBluetooth output commandを送る
3. 列挙とread-only診断だけを記録し、直接Bluetooth対応を保留する

### 決定

3を採用する。診断用Raw Input monitorは残すが、production transportにはしない。

### 理由

Bluetoothではvendor raw collectionがなく、純正software停止後もphysical keyの
down/upをACK05 sourceとして取得できなかった。source不明の合成keyboard eventは
他deviceと安全に区別できず、momentary layerの入力には使えない。

### 影響

v1の確実な実機transportは有線USBになる。付属receiverは未所有のため未検証。
Bluetooth接続自体や純正softwareの通常利用は変更しない。

### 戻し方

将来、識別可能なBluetooth state reportまたは安全なtransportが見つかった場合、
Coreのlayer semanticsを変えずadapterとして追加する。

## 2026-07-16: 再接続supervisorをOS非依存Coreへ置く

### 背景

Probeは1回のFileStreamだけを監視し、USB切断で終了していた。production appでは
device absent、切断、再接続、process終了の全経路で同じstate resetが必要になる。

### 選択肢

1. Probeのwhile loopにWindows固有の再接続処理を直接追加する
2. Coreに抽象report sourceとsinkを置き、Windows HIDをadapter化する
3. GUI実装まで再接続を延期する

### 決定

2を採用する。1接続ごとに`Ack05InputSession`を作り、`Ack05InputService`がsourceの
探索、読取、reset、dispose、再接続を管理する。

### 理由

fake transportで切断と再接続を決定的にテストでき、将来Bluetooth transportを追加
してもlayer semanticsと安全処理を変えずに済む。

### 影響

disconnectとshutdownのreset eventはcancel済みtokenではなく
`CancellationToken.None`でsinkへ渡す。raw-mode commandの送信可否はWindows factory
作成時の明示optionに固定し、再接続ごとに同じ選択を適用する。

### 戻し方

service interfaceを維持したままsource factoryだけ交換できる。問題があれば`watch`
commandを外し、既存のsingle-session `monitor`を残す。

## 2026-07-16: logical actionをversion 1 JSON contractとしてCoreへ置く

### 背景

SendInput実装前に、profile、layer、binding、actionの意味をWindows APIやGUIから独立して
固定する必要がある。将来fieldを追加しても、古い版で設定を開いただけで消失させたくない。

### 選択肢

1. GUI実装時まで保存形式を決めない
2. Windows virtual-key値を直接JSONへ保存する
3. OS非依存のlogical key/action modelとversion付きJSONをCoreへ置く

### 決定

3を採用する。schema v1はLayer 0/1、K2-K10、roller左右、`disabled`、
`keyboardChord`、`mouseWheel`を定義する。K1はmomentary modifier専用としbindingを拒否する。

### 理由

Core testだけでvalidationとserializationを固定でき、Windows output adapterやGUIを交換しても
設定の意味が変わらない。未知JSON fieldはextension dataとして往復保持し、未知versionや
不正payloadはfail closedにできる。

### 影響

keyboard chordはlogical keyの配列順をdown順とし、outputは逆順releaseを必須とする。
file path、atomic save、backup、corrupt recovery、migrationはまだ実装しない。

### 戻し方

application層はCore modelだけを参照する。schema変更が必要なら既存v1 parserを残し、
新versionへの明示migrationを追加する。

## 2026-07-16: action reference countとSendInput registryを二重化する

### 背景

複数ACK05 keyが同じCtrl/Shiftを共有すると、先に離したactionがmodifierをreleaseしては
ならない。またCore resetが正しくてもWindowsが全eventを受理した保証にはならない。

### 選択肢

1. bindingごとにchord全体を毎回down/upする
2. Coreだけでpressed keyを管理する
3. Coreでlogical reference count、Windows adapterで送信成功registryを別々に持つ

### 決定

3を採用する。Coreは共有keyを最初の参照でDown、最後の参照でUpにする。Windows adapterは
実際にSendInput成功したDownだけを逆順release可能なregistryへ記録する。

### 理由

action semanticsとOS送信成否を分離できる。disconnect、profile change、exception、Ctrl+C、
disposeのどこでも二つのstateをresetでき、release失敗時も残りのkeyをbest effortで試せる。

### 影響

`run`は`--enable-bitmask`と`--allow-sendinput`の両方を必須にする。Windows key mappingは
configurationへvirtual-key数値を保存せず、adapter内でlogical enumから変換する。

### 戻し方

Core output event contractを維持し、SendInput injectorだけ別実装へ交換できる。

## 2026-07-16: foreground process名を正規化してprofile選択する

### 背景

.NETの`ProcessName`は通常`.exe`なしだが、ユーザー向け設定では`AfterFX.exe`のような
実行file名が自然である。profile切替中にchordが保持されている可能性もある。

### 選択肢

1. 完全一致だけにする
2. pathまたはwindow titleで判定する
3. process file名をcase-insensitiveかつ`.exe`任意で正規化する

### 決定

3を採用し、foreground processを250ms間隔で確認する。同じ正規化名を複数profileへ設定
することはvalidationで拒否する。不一致・取得失敗時はdefault profileへ戻す。

### 理由

pathやwindow titleより設定が安定し、権限差の影響も小さい。`.exe`表記差で意図せずdefaultへ
戻る問題を避けられる。切替前にはaction engine resetとWindows release-allを実行する。

### 戻し方

Core selectorとforeground providerは分離しているため、将来package identityなど別ruleを
追加してもSendInput/action engineを変更しない。
## 2026-07-16: Vial-like UIはまずfile-oriented keymap editorとして実装する

### 背景
ユーザーはホイール、レイヤー、トグル設定まで含めて、VialのようにGUIでキーマップ編集できる方向を希望した。

### 選択肢
1. Tray常駐、device service、live outputまで一体のGUIを作る
2. 既存JSON contractを直接編集する独立Windows Forms editorから始める
3. UI実装を止めて仕様だけ作る

### 決定
2を採用する。`Ack05Enhanced.App` はCoreだけを参照し、device raw modeやSendInputを開始せず、明示的に選んだJSON fileだけをopen/saveする。

### 理由
既存の安全境界を保ったまま、profile、Layer 0/1、K2-K10、roller、keyboard chord、mouse wheelをすぐ編集できる。Vial風の物理配置、key capture、toggle layerは入力セマンティクスやUI品質への影響が大きいため後続に分ける。

### 影響
設定編集の入口ができる一方、まだ常駐tray、live reload、default per-user config、toggle layer動作は持たない。

### 戻し方
`Ack05Enhanced.App` projectとsolution登録を削除すれば、Core、Probe、Windows outputの既存動作へ影響せず戻せる。

## 2026-07-16: K1 layer controlは3 modeを後方互換で追加する

### 背景
開発ハブの最優先要件は、Vial/QMK/ZMKに近いlayer toggle、layer shift、単押し／長押し判定をGUIから設定できることだった。既存schema v1はK1をmomentary専用としていた。

### 選択肢
1. schema versionを上げ、profileごとに任意のlayer actionを定義する
2. schema v1へ全profile共通のoptional `layerSwitch`を追加する
3. UIだけ追加し、runtime動作は後続にする

### 決定
2を採用する。modeは`momentary`、`toggle`、`tapToggleHold`の3種とし、thresholdは100-1000 ms、既定200 msとする。`layerSwitch`がない既存JSONはMomentaryとして読む。

Tap Toggle / Hold ShiftはK1 down直後からLayer 1を使えるようにし、threshold未満かつ他control未使用のreleaseだけをtap toggleと判定する。長い保持またはK1との組み合わせはhold shiftとして、release時に直前のtoggle状態へ戻す。

### 理由
既存設定を壊さず、物理操作に遅延を加えず、単押しと組み合わせ操作を決定的に区別できる。K1を全profile共通にすることで、foreground profile切替より前段にあるinput sessionの責務も保てる。

### 影響
Core router、設定validator、CLI configured run、GUI Liveが同じmodeとthresholdを使う。Live中のmode変更は保持出力とrouter stateを安全にresetするため、Stop/Startを自動実行する。任意のtap actionとprofile別K1 modeは今回含めない。

### 戻し方
設定の`layerSwitch`を削除または`momentary`へ戻せば従来挙動になる。実装を戻す場合もschema versionは1のままで既存profile/layer/actionを維持できる。

## 2026-07-16: layer controlはK1-K10から全profile共通で選ぶ

### 背景
K1以外もlayer shift/toggleに使いたい一方、選択したkeyの既存Layer 0/1 bindingを
消すとkeymap編集の安全性と可逆性が損なわれる。

### 選択肢
1. profile・layerごとに任意のlayer actionを追加してschemaを再設計する
2. 既存の全profile共通`layerSwitch`へK1-K10の`control`を追加する
3. 新controlのbindingを警告なしで削除する

### 決定
2を採用する。`control`はK1-K10、既定K1とする。GUIで変更すると全profile・両layerで
新controlのactionを旧controlへ移し、新controlを予約する。戻す操作ではbindingも元の
controlへ戻る。roller方向と未検証のroller centerはlayer control候補に含めない。

### 理由
schema v1とrouterの責務を維持したままK1以外を選べ、既存actionを破棄しない。
input routingはprofile選択より前段なので、全profile共通なら接続sessionごとのstateも一意になる。

### 影響
選択中のcontrolだけbinding不可となり、K1を含む残り9 keyは通常actionを持てる。
Live中のcontrol変更はheld outputとlayer latchを解放するため安全なStop/Startを行う。
profile別layer controlや3層以上の任意layer actionは将来拡張とする。

### 戻し方
TriggerをK1へ戻せば従来操作とbinding配置へ戻る。JSONから`control`を省略してもK1として読む。

## 2026-07-16: physical mapは既存binding editorのselectorとして追加する

### 背景
Vialのように本体を見ながら設定する体験には、control名だけの表より物理配置が必要である。一方、別の設定stateを持つと表とmapが食い違う。

### 選択肢
1. DataGridViewを完全に廃止してmapだけにする
2. mapを選択・要約表示にし、既存表とaction editorへ同期する
3. 装飾用のbitmapだけ表示する

### 決定
2を採用する。ACK05の実機参照図に合わせ、左に大型roller、右にK1-K3／K4-K6、
右端へ縦長K7、下段へK8／横長K9／K10を配置する。各keyとroller左右はclickで表を
選択し、actionはCore modelから毎回再描画する。中央roller switchはprotocol bit未確認のため
PENDING表示だけとする。

### 理由
既存の編集・validation・Live反映を再利用しながら、物理位置から最短でcontrolを選べる。未検証の中央buttonを割当可能に見せる誤解も避けられる。

### 影響
既定windowを1180x820、minimumを980x700へ広げ、device mapへ300pxを確保する。
物理形状とresponsive boundsはpresentation層へ閉じ込め、JSONとruntime semanticsは変更しない。

### 戻し方
`Ack05DeviceMapControl`とbindings panel中央行を削除すれば、既存DataGridView editorへ戻せる。JSONとruntimeには影響しない。

## 2026-07-16: key captureはpreview後に既存Applyへ渡す

### 背景
モディファイアと通常キーをチェックリストから探す操作は遅く、Vialに近い編集体験には実際のshortcut入力が必要である。一方、capture直後にbindingを確定すると誤入力を取り消しにくい。

### 選択肢
1. 押した瞬間にbindingへ保存する
2. modalでpreviewし、Use後も既存`Apply Binding`を確定点にする
3. Windows virtual-key値をJSONへ直接保存する

### 決定
2を採用する。WinForms `KeyDown`をlogical `Ack05KeyboardKey`へ変換し、modifier＋main keyをpreviewする。Useでeditorへ転記し、Apply Bindingで初めてin-memory configurationを変更する。capture順序は保持し、manual checklistはmodifier-firstへ正規化する。

### 理由
誤入力をCancelでき、既存validation・Live update・logical key contractをそのまま使える。Windows固有のkey codeをCoreやJSONへ漏らさない。

### 影響
Numpad EnterなどWinFormsの通常KeyEventだけで区別できないkeyは既存checklistから設定する。capture自体はSendInputやdevice writeを行わない。

### 戻し方
`KeyCaptureDialog`とCapture buttonを削除すれば、checklist editorへ戻せる。保存形式は変わらない。

## 2026-07-16: schema v2はレイヤーを先に作り、操作を任意キーへ割り当てる

### 背景

全profile共通の予約キーでは、Layer 0のK2=A、K1保持中のK2=Bのような設定は
できても、複数レイヤーを先に用意し、あとから任意位置へlayer switchを置く
Vial / Keymap Editor型の編集姿勢にならない。

### 選択肢

1. schema v1のglobal `layerSwitch`を拡張して対象レイヤーだけ増やす。
2. layer switchをbinding actionへ移し、active layer stackとTransparentを導入する。
3. firmware互換の完全Vial protocolを実装する。

### 決定

2を採用する。schema v2はprofileごとに2-8枚、新規4枚とし、全K1-K10を
通常actionまたは `MO(n)` / `TG(n)` / Tap Toggle / Hold Layerへ割り当てられる。
高いactive layerを優先し、Transparentは下位active layerへ透過する。
rollerはUpを持たないためTGだけを許可する。

schema v1はload時に自動移行し、旧Momentary/Toggle/Tap-HoldをLayer 0上の
対応actionへ変換する。既存Layer 0/1 bindingは保持し、Layers 2/3をTransparentで
追加する。保存はschema v2とする。

### 理由

レイヤー数とlayer control位置を分離でき、K1を含む全キーを同じ編集モデルで
扱える。CoreのWindows非依存性とdevice write境界を変えずに、Vialの基本的な
layer stack / Transparent / MO / TGの考え方へ揃えられる。

### 影響

Live/CLIはforeground profile選択後にprofile-aware routerを通す。config/profile
変更時は保持出力とactive layerをpanic releaseしてからrouterを交換する。
最初のv2ではTO/OSL/LT/LM/true TTを含めない。

### 戻し方

schema v1の読み込み互換はmigratorで維持する。実装を戻す場合もschema v2 JSONを
v1へ自動downgradeはしないため、保存前の `.bak` またはGitから復元する。

## 2026-07-17: action kindとkeycodeは一つの折りたたみpaletteで選ぶ

### 背景

従来UIはActionをDisabledからKeyboardChordへ切り替えないとkeyを選べず、
状態selectorとkey checklistが離れていた。ユーザーはAE Effects & Presetsのような
密度の高い折りたたみ分類を参考に、DisabledやTransparentも同じ選択面へ置くことを希望した。

### 選択肢

1. 既存Action ComboBoxを残し、keyboard checklistだけ分類する。
2. 全actionとkeycodeを同じTreeView paletteへ統合する。
3. Vial互換protocolとweb UIを先に導入する。

### 決定

2を採用する。Binding State、keyboard分類、Layer / Device Actionsをnative TreeViewの
折りたたみcategoryとして表示する。keyboard keyは複数選択できる。Disabled、Transparent、
Mouse Wheel、MO、TG、Tap Toggle / Hold Layerは単独actionとし、選択中は他候補をgray outする。
同じ単独actionを再度押すと解除し、別のactionを選べる状態へ戻す。

### 理由

Action kindを先に変更する余計な手順をなくし、通常keyと特殊actionを同じ探索モデルで扱える。
既存WinFormsの描画、config schema、runtime semanticsを変えずにVial型のpaletteへ近づけられる。

### 影響

keycode検索はまだない。roller pulseではupを必要とするMOとTap Toggle / Holdを無効表示し、
Mouse WheelとTGは選択できる。CaptureとApplyの有効状態はpalette選択から決まる。

### 戻し方

`Ack05ActionPalette`を削除し、`KeymapEditorForm`のAction ComboBoxとCheckedListBoxへ戻す。
保存形式とCore actionは変更していないためconfig migrationは不要。

## 2026-07-17: 次期Action選択は一押し置換を優先する

### 背景

統合Action PaletteでDisabledなどの単独actionを選択すると他候補をgray outし、
いったん解除してから次のactionを選ぶ必要があった。これは誤操作防止にはなるが、
Vial型の「物理キーを選び、次のactionを一度押す」操作より一手多い。

### 選択肢

1. 現行どおり単独actionを解除してから別actionを選ぶ。
2. 別の有効actionを押した時点で現在actionを自動解除し、選択を置き換える。
3. 2に加えてApply Bindingを廃止し、action選択を即座にdraftへ反映する。

### 決定

最終目標は3とする。別actionは常に一押しで置き換え、単独actionであることだけを理由に
他候補をgray outしない。gray outはroller上のMOなど、context上実行できない候補だけに使う。
ただしApply Bindingの廃止はUndo/Redo、dirty表示、saved/draft区別を実装した後に行う。
それまでの中間実装では2を採用してよい。

### 理由

通常キー割り当ての最短操作を守りながら、直接編集で必要な復元手段を先に用意できる。
Tap & HoldやComboのaction chooserも同じ置換規則を再利用できる。

### 影響

2026-07-17の統合palette判断にある「単独action選択中は他候補をgray outする」部分は、
この判断で置き換える。checkbox風表示も将来は単独actionとkeyboard chordの違いが分かる
button/radio表現へ見直す。詳細は
`docs/design/ADVANCED_BINDINGS_UI_PLAN.md`を参照する。

### 戻し方

自動置換を無効にし、現行の解除後選択へ戻せる。schemaとruntime semanticsは変わらない。
Apply廃止後に戻す場合は、Undo historyを残したまま明示Applyを再導入する。

## 2026-07-17: Action Paletteはclickとdrag-and-dropの両方で割り当てる

### 背景

Vial型のUIでは物理keymapとaction一覧が同じ画面にあり、actionを見ながら対象keyへ
直接置ける構造になっている。ACK05でも一覧から選んだ後に別のApply操作を要求するより、
actionそのものを物理keyへdragする操作が自然である。

### 選択肢

1. click-to-assignだけを提供する。
2. D&Dだけを提供する。
3. click-to-assignを残し、D&Dを同じdraft変更へのshortcutとして追加する。

### 決定

3を採用する。palette上のBasic、Layer、再利用Tap & Hold、User actionを物理keyへ
dragできるようにする。dropはin-memory draftへ即時反映し、1回のUndo操作で戻せる。
file writeはSaveだけが行う。

### 理由

D&Dは物理配置を見ながら複数keyを設定する作業に速い。一方でkeyboard操作、精密pointer、
accessibility、誤dropからの復元にはclick経路とUndoが必要であり、D&Dだけにはできない。

### 影響

無効なdrop targetは単に反応しないのではなく、禁止cursorと理由を表示する。
leaf keyboard keyのdropはsingle-key chordへ置換し、複数key chordを暗黙に加算しない。
physical key同士のcopy D&DとCombo badgeへのdropは後続とする。詳細は
`docs/design/ADVANCED_BINDINGS_UI_PLAN.md`を参照する。

### 戻し方

drag source/target handlerを無効にしてもclick-to-assignは残る。draft action contract、
schema、runtime semanticsには影響しない。

## 2026-07-17: Tap/Holdは固定layer presetから再利用可能なTap Dance定義へ進める

### 背景

現行 `TapToggleHoldLayer` は短押しTG、長押しまたは別キー併用MOへ固定され、
単押し側と長押し側へ任意actionを設定できない。Action、Target Layer、thresholdが
同じeditorへ並ぶだけなので、ひとつのdual-role behaviorとしても読み取りにくい。

### 選択肢

1. 現行actionへTap用keyboard chordだけを追加する。
2. key bindingへTap/Hold payloadを直接埋め込む。
3. Vial型の再利用可能なTap Dance定義を作り、key bindingから参照する。

### 決定

3を次期要件の方向とする。Tap Dance定義はTap、Hold、将来のDouble Tap、Tap + Hold、
tapping term、interrupt modeを持つ。keymapから定義を選ぶVial型の操作に加え、
選択中キーから定義作成と割当を一度に行う `Create New... / Apply and Assign` を用意する。

最初の実装sliceはTap/Holdと3種類のinterrupt modeに限定する。Double Tapと
Tap + Holdは同じmodelへ後から追加する。詳細は
`docs/design/TAP_DANCE_REQUIREMENTS.md`を正とする。

### 理由

TapとHoldのactionを独立させながら、同じ定義を複数キー／レイヤーから再利用できる。
Vialの構造へ寄せても、quick createにより単純なTap/Hold設定を二段階操作にしない。
判定方法と実行actionを分離するため、将来のkeycode paletteやmacroにも拡張しやすい。

### 影響

composite actionとtimer-driven resolverにはschema v3が必要になる見込みである。
既存schema v2と `tapToggleHoldLayer` はsilent conversionせず従来挙動を維持し、
明示変換previewだけを追加する。Hold action開始後は物理Up、Stop、disconnect、
profile/config変更、終了のすべてでmatching releaseを保証する。

### 戻し方

実装前はこの要件documentとTODOを取り下げればschema v2へ影響しない。実装後も
legacy actionとschema v2 loaderを残し、新しいTap Dance bindingを使わないconfigは
従来routerで動かせる境界を維持する。

## 2026-07-17: advanced editorは即時Live draft、2-key Combo、Vial型Tap Danceを採用する

### 背景

Apply Binding廃止後のLive反映、ComboとTap/Holdの判定順、Save後のUndo、クラッシュ復旧、
同じmodifierを複数actionが保持した場合のrelease責務を確定する必要がある。

### 選択肢

1. 編集はSaveまでLiveへ反映せず、ComboとTap/Holdも独立に処理する。
2. valid draftをLiveへ即時反映し、Vial/QMKに近いCombo先行・Tap Dance後段のresolverを持つ。
3. 3-key以上のComboと再帰的composite actionまで最初から実装する。

### 決定

2を採用する。Saveはfile writeだけを担当し、valid draft変更はLive runtimeへ即時反映する。
Revert to Savedで最後の明示Saveへ戻せる。Save後も起動中のUndo/Redo履歴を保持し、Layer削除も
参照を含む1 transactionとして復元する。working draftはLocal App Dataへ自動保存し、異常終了後に
復旧を提示する。

Tap DanceはVialと同じOn Tap / On Hold / On Double Tap / On Tap + Holdの将来modelを採用し、
初期UIはTap/Holdだけを公開する。Comboはexactly 2 keys、strict term、first-member layer snapshotとし、
同一timestampで複数pairが成立する3-key入力はfail closedで通常入力へ戻す。

action capabilityはCombo timingとは別のCore metadataとする。同じlogical outputを複数actionが
保持する場合はowner tokenで参照数を管理し、最後のownerが離れた時だけmatching Upを送る。

### 理由

通常編集は一操作で実機確認でき、Save済み状態は常に安全な戻り先になる。2-key限定とfail-closed
競合規則により、device report内のbit列挙順に依存しない。capabilityとownerをCoreへ集約すると、
Keymap、Tap、Hold、Combo、rollerのUIとruntime validationが一致する。

### 影響

Live hot-swap時はpending inputと保持出力をrelease/resetする。Combo member自身のMO/Tap & Holdは
Combo判定中にlayerを変えず、失敗後に元timestampでreplayされる。3-key以上のComboとnested
composite actionは初期schema v3に含めない。

### 戻し方

Live即時反映を無効にして明示Apply to Liveへ戻せる。Combo機能を無効化しても通常bindingと
Tap & Hold resolverは維持する。recovery draftを無視すれば最後のSave済みJSONから再開できる。

## 2026-07-17: Combo判定parameterは基本項目と折りたたみ詳細から編集する

### 背景

Combo Termだけでなく、入力順、tap/hold条件、release条件も誤発火と操作感を左右する。
JSON直接編集を要求せず、Vial/QMK系の調整可能性を保ちながら通常画面を煩雑にしない必要がある。

### 選択肢

1. Combo Termだけを固定値として実装する。
2. すべてのparameterを常時表示する。
3. Termを基本欄、order/trigger/hold/releaseを折りたたみ詳細へ置く。

### 決定

3を採用する。profile既定Termとper-Combo overrideを持つ。詳細欄でAny/ordered、Press/Tap only/
Hold together、条件付きHold Term、First/All member releaseを編集する。3-key競合のfail-closedと
exactly-two-key制約は安全規則として固定し、UI設定にはしない。

numeric入力はspinner step、Enter、focus commitを1 gestureとし、1 Undoと1 Live hot-swapへまとめる。
Live中は直近chordの順序、実測ms、match結果をread-only表示する。

### 理由

通常ユーザーはTermだけで調整でき、必要な場合だけQMK相当の判定条件へ進める。実測値が見えるため、
感覚だけでTermを調整せずに済む。固定安全規則を隠しparameterにしないことで再現性も保てる。

### 影響

schema v3 Comboへ `ordered`、`trigger`、`holdTermMs`、`release`を追加する。parameter確定時の
Live hot-swapはpending inputと保持outputを安全にreleaseしてから行う。

### 戻し方

詳細欄を無効化し、全ComboをAny order / On second press / First member releaseへ正規化すれば、
Termだけの最小resolverへ戻せる。
## 2026-07-17: Instrument Workbench commits valid edits immediately

### Background

The selected visual direction placed the hardware map in the center, a dedicated
selected-key inspector on the right, and reusable actions in a bottom dock. The
desired interaction was one click for ordinary assignment, with Save remaining
explicit and Revert available as a safety net.

### Options

1. Keep the legacy three-column editor and explicit Apply Binding.
2. Restyle only, but retain pending drafts and no history.
3. Adopt the workbench shell and make valid edits immediate behind session
   Undo/Redo, dirty-baseline tracking, and Revert to Saved.

### Decision

Use option 3. Action-card clicks, relevant inspector parameters, and valid
K1-K10 drops replace the in-memory binding immediately. Save writes the current
configuration and moves the saved baseline without clearing history. Revert is
an undoable edit. A collapsed `Apply current draft` button remains as a recovery
affordance while the immediate path stabilizes.

### Reason

This matches the Vial/Keymap Editor posture, removes the extra Apply step, and
keeps Live behavior synchronized without making Save a device or runtime action.
The dedicated inspector explains the selected key and hides irrelevant fields,
which offsets the visual abstraction of the minimal workbench.

### Impact

Schema v2 and Core routing do not change. Tap & Hold and Combo tabs are stable UI
boundaries only until schema v3 exists. Session history is cleared on Open, New,
or process exit; abnormal-exit draft recovery remains future work.

### Rollback

Disable automatic calls from the palette/parameter events and expose the
Advanced Apply button by default. The action model and persisted JSON remain
compatible.

## 2026-07-17: Implement Tap & Hold before Combo on a shared resolver boundary

### Background

Both Tap & Hold and Combo delay individual key output while timing decides the
meaning of physical input. Tap & Hold has the smaller state space and supplies
the interrupt, timer, override-action, and panic-release foundations needed by Combo.

### Options

1. Implement Combo first and keep Tap & Hold as the legacy layer-only action.
2. Implement both features in one schema/runtime/UI rewrite.
3. Land reusable Tap & Hold as schema v3, verify its resolver, then build exactly-two-key Combo on it.

### Decision

Use option 3. Schema v3 adds profile-owned Tap & Hold definitions and stable
`tapHoldId` bindings. The first slots allow leaf actions only. The router resolves
three interrupt modes and exposes `Tick()`; Windows and Probe hosts call it every
10 ms. Combo remains the next isolated slice.

### Reason

This makes `Tap=B / Hold=Shift` and `Tap=B / Hold=MO(1)` independently testable
before member-event buffering is introduced. It also preserves the safety rule
that a confirmed Hold resolves before an interrupting key is routed.

### Impact

Schema v1 and v2 load into v3 without converting legacy `tapToggleHoldLayer`.
Referenced definitions and target layers cannot be deleted. Live configuration
replacement cancels pending decisions through the existing panic-release path.

### Rollback

Disable Tap & Hold assignment in the UI and return schema serialization to v2.
Legacy bindings and layer actions remain intact because they were not converted.

## 2026-07-17: Resolve exactly-two-key Combo before Tap and ordinary bindings

### Background

Combo members may themselves be MO or Tap & Hold keys. Letting those bindings
run before Combo recognition can change the eligible layer or leak output that a
successful Combo is supposed to suppress.

### Options

1. Emit both member bindings immediately and cancel them if a Combo later matches.
2. Buffer every physical key globally for the longest Combo term.
3. Buffer only keys that begin an eligible two-key Combo, snapshot the active
   layer, and replay failed candidates into the normal resolver.

### Decision

Use option 3. The first member selects candidates from its active-layer snapshot.
The second member must arrive within the effective profile/per-Combo term. Press
matches wait for the existing 10 ms host tick before confirmation, allowing a
third key in the same input batch to fail closed. Tap-only and Hold-together use
their explicit Hold Term. A match suppresses member bindings; failure replays the
buffer with captured timestamps.

### Reason

This preserves deterministic layer scope, avoids synthetic key cancellation, and
delays only keys that are actual Combo candidates. It also composes with the
already tested Tap & Hold interrupt resolver instead of duplicating it.

### Impact

Schema v3 profiles now own `comboDefaults` and `combos`. Combo output is limited
to leaf Disabled, Keyboard Chord, Mouse Wheel, MO, and TG actions. Definitions
have exactly two K1-K10 members; rollers and overlapping duplicate pairs are
rejected. Reset discards pending candidates and releases active held output.

### Rollback

Remove profile Combo definitions or disable the Combos workspace. With an empty
`combos` array, every input follows the pre-Combo Tap & Hold/binding path unchanged.

## 2026-07-17: Give the shared Action Palette resizable vertical space

### Background

The fixed 214 px action dock showed only one complete card row. Normal Keys often
placed the next row behind the footer, so assigning a key required precise
scrolling even when the application window had unused vertical space.

### Options

1. Increase the fixed dock height for every workspace.
2. Move the action library into a separate dialog.
3. Split the editor and action library horizontally, give extra window height to
   the library, and let the user drag the divider.

### Decision

Use option 3. The upper editor is the fixed panel during window resize. The lower
Action Palette starts at a comfortable multi-row height, has a 160 px minimum,
keeps its own scrolling, and can be resized directly. Profiles collapses the lower
panel because it does not use the action chooser. The splitter retains an 8 px
drag target, but paints only a 42 x 4 px centered rounded handle using the existing
border color; the remaining splitter area matches the canvas.

### Reason

The physical map stays in context while the assignable choices become easier to
scan. The same control works for Keymap, Tap, Hold, and Combo without duplicating
lists or adding a modal step.

### Impact

The default map area is slightly shorter than the former fixed-dock layout. Empty
Tap & Hold and Combo states disable the palette with a direct instruction, and the
Combo Layer Scope checklist reserves enough height for all four default layers.

### Rollback

Replace the `SplitContainer` with the previous fixed row and restore the 214 px
height. The configuration model, runtime semantics, and saved JSON are unchanged.

## 2026-07-18: Establish hardware-free Windows CI before public hosting

### Background

The repository has deterministic self-tests but no remote repository or clean-
checkout verification. Personal root configuration files are also visible as
untracked files, which makes an accidental first public commit more likely.

### Options

1. Wait until the GitHub owner, visibility, and license are chosen.
2. Add repository-neutral Windows CI and source hygiene now, while deferring the
   hosting and license decisions.
3. Publish binaries manually without a clean-checkout gate.

### Decision

Use option 2. Ignore personal root config/backup/temp files and add a Windows-
latest workflow that restores .NET 8, builds Release, and runs the deterministic
self-test executable. Add source-build installation, user, security, and release
checklists without choosing a license or claiming signed packaging.

### Reason

These checks require no ACK05 hardware, reduce accidental data exposure, and are
valid for either a public or private repository. Ownership, visibility, license,
signing, and release channels remain explicit user decisions.

### Impact

The workflow becomes active after the repository is hosted on GitHub. Local build
and runtime behavior do not change. The documentation describes the current
source-preview boundary and does not promise a packaged release.

### Rollback

Remove the workflow and release documents, then remove the root config ignore
rules. No application code or saved schema is affected.

## 2026-07-18: Show current-layer Combo badges on the Keymap

### Background

Combos could be created and edited, but the main Keymap did not reveal that a pair
existed. This made a saved layout harder to read and required switching workspaces
to discover or reopen a definition.

### Options

1. Keep Combo visualization only in the Combos workspace.
2. Draw every connector permanently on the Keymap.
3. Show compact current-layer output/scope badges and reveal member emphasis only
   on hover or keyboard focus.

### Decision

Use option 3. The Keymap derives badges from definitions whose layer scope includes
the selected layer. Badges use the existing monochrome theme, expose accessible
names/tooltips, emphasize both endpoint keys and a dotted connector on hover or
focus, and open the selected definition in the Combos workspace when activated.

### Reason

The layout becomes self-describing without turning the small ACK05 map into a web
of permanent lines or adding another color. The Combo list remains the complete,
non-spatial editing path.

### Impact

This is presentation-only and does not alter schema or runtime ordering. Nearby
horizontal pairs place the badge above the keys when space permits; collisions are
nudged within the device frame. More advanced overlap rails and an All Layers view
remain future UI work.

### Rollback

Call the map without Combo display and remove the badge activation event. Combo
definitions and runtime behavior remain unchanged.

## 2026-07-18: Persist recovery drafts separately from explicit Save

### Background

Immediate editing and Live preview intentionally make Save the durable-baseline
operation, but process exit previously discarded every unsaved edit. The required
recovery model needs two persisted states without treating autosave as Save.

### Options

1. Autosave directly over the user's JSON.
2. Keep only session Undo/Redo and accept loss on exit.
3. Store a separate per-user recovery envelope containing the draft and explicit-
   Save baseline.

### Decision

Use option 3. Dirty changes debounce into Local App Data. The envelope includes the
working configuration, saved baseline, optional source path, source fingerprint,
and UTC timestamp. Startup validates it and offers Restore or Discard. Restoring
never starts Live and does not attempt to persist session history. Clean state
deletes the envelope.

Before overwriting the original source path, compare its current SHA-256 fingerprint
with the loaded/saved fingerprint. A mismatch requires Overwrite, Save As, or Cancel.
Corrupt recovery data is quarantined and cannot block startup.

### Reason

This preserves the common meaning of Save, allows Revert after recovery, avoids
writing beside arbitrary user files on every edit, and prevents an old draft from
silently overwriting a file changed by another tool.

### Impact

The application writes one per-user recovery file while dirty. Undo/Redo remains
session-only. Recovery contains configuration data including process match names,
so it stays outside Git under the user's Local App Data directory.

### Rollback

Disable recovery scheduling and startup restore. Explicit Save, `.bak`, Undo/Redo,
and Live hot-swap continue to work independently.

## 2026-07-20: Use the directional Resolve Edit keymap as the implementation baseline

### Background

The DaVinci Resolve Edit-page profile needs a stable physical grammar before its
semantic commands are converted into a configuration JSON and Resolve keyboard
preset. The first proposal spread transport and layer controls across the device,
but the preferred operation is K2=up, K4=left, K5=down, K6=right, with K9 as Space
and K7/K8/K10 reserved for layer-oriented control.

### Options

1. Preserve the first transport-oriented J/K/L layout.
2. Use K1-K6 as a directional pad, K9 as invariant Play/Stop, and K7/K8/K10 as
   momentary Trim, Assemble, and Utility layer keys.
3. Make K7/K8/K10 dual-purpose Combo and layer keys immediately.

### Decision

Use option 2 as the implementation and evaluation baseline documented in
`docs/design/DAVINCI_RESOLVE_EDIT_KEYMAP.md`. K2/K4/K5/K6 retain directional
meaning across layers, K9 emits Space on every layer, and K7/K8/K10 initially use
momentary layers. Mutual layer-key Combos remain disabled until timing is verified
on hardware.

### Reason

A stable directional grammar is easier to operate without looking at the device.
Keeping Space invariant makes playback cancellation predictable. Deferring mutual
layer-key Combos avoids recognition buffering and timing-dependent ambiguity
between a layer chord and a Combo.

### Impact

The first Resolve profile, configuration example, and test plan should derive from
this map. Resolve command names remain the source of truth until the actual Windows
keyboard preset is verified. This decision does not yet change runtime behavior,
the configuration schema, device transport, or user data.

### Rollback

Mark the design as superseded, document the tested replacement map, and update the
Resolve profile artifacts together. No migration is needed until a configuration
JSON is published.

## 2026-07-20: Keep the physical map visible while editing reusable behaviors

### Background

Tap & Hold currently replaces the physical keymap with a definition form. The
definition can show a reference count, but the user cannot see which layer and key
use it or choose an assignment target in the same spatial context. Combo editing
already demonstrates that a compact map and a contextual inspector can coexist.

### Options

1. Keep separate full-page editors and add text-only reference locations.
2. Add an optional map popup or Overview button to Tap & Hold.
3. Keep a compact physical map visible and change the surrounding list/inspector
   for each editing mode.

### Decision

Use option 3. Begin by moving Tap & Hold to the existing Combo-style three-column
layout. Keep the definition list compact, show a current-layer map in the center,
and place definition settings on the right. Show exact assignment locations and
resolved Tap/Hold meaning. Preserve the resizable Action Palette below.

### Reason

The physical layout is the stable mental model across layers and advanced
behaviors. Keeping it visible removes reliance on memory, makes assignment targets
explicit, and establishes one coherent direction for future Tap Dance and macro
editing without changing runtime semantics.

### Impact

Tap & Hold gains a visible map and direct target selection. Map rendering resolves
known Tap & Hold definitions into their Tap/Hold actions. The configuration schema,
Live runtime, output safety, and saved data remain unchanged. A later refactor may
share one map instance across workspaces.

### Rollback

Restore the two-column Tap & Hold layout and the internal-ID map label. Definitions,
bindings, runtime behavior, and saved files remain valid because this is a UI-only
change.

## 2026-07-21: Use a dedicated compact workspace selector

### Background

At 1081 x 741, the fixed brand/action header columns left less width than the four
non-wrapping workspace pills required. WinForms clipped Combos and hid Profiles
under the separate file `•••` control, leaving no valid pointer navigation path.

### Options

1. Shrink labels, spacing, or the fixed action row until the four pills fit.
2. Merge hidden workspaces into the existing file `•••` menu.
3. Atomically replace all workspace pills with a dedicated active-workspace menu
   whenever their navigation container is too narrow.

### Decision

Use option 3. Measure the available navigation client width and actual pill widths
plus margins. Show all four direct pills when they fit; otherwise show one selector
labelled with the current workspace and a checked menu containing all four choices.

### Reason

Atomic replacement prevents partial controls at any breakpoint. A dedicated menu
keeps navigation separate from file commands, preserves a visible sense of place,
and remains stable when DPI or future header changes alter measured control widths.

### Impact

Compact navigation takes two clicks instead of one but every workspace remains
visible, accessible, and reachable. The existing `ShowWorkspace` method remains
the only state transition. Config, persistence, Undo, Output, runtime, and file
menu behavior are unchanged.

### Rollback

Remove the compact selector/menu and responsive-mode method. No saved data or
configuration migration is involved.

## 2026-07-21: Treat the verified roller center as a stateful assignable control

### Background

The editor showed the roller center as pending because its raw-report position
had not been verified. A wired Windows capture now shows byte 3 bit 2 set while
the center is held and cleared on release across repeated isolated presses.

### Options

1. Keep the center unavailable despite the hardware evidence.
2. Expose it only as a simple keymap binding.
3. Model it as a normal stateful control available to keymap, layers, Tap & Hold,
   and Combos while keeping rotation directions pulse-only.

### Decision

Use option 3 and name the persisted control `RollerPress`. Preserve the numeric
values of existing `Ack05Control` members and append the new value. Older profiles
are normalized with a disabled binding, so the schema version does not change.

### Reason

The verified report supplies distinct Down and Up transitions, which satisfy the
same lifecycle and cancellation guarantees as K1-K10. Restricting the control to
tap-only behavior would discard verified capability and create an unnecessary
special case.

### Impact

The center can hold keyboard output or a momentary layer and can participate in
Tap & Hold and exactly-two-control Combos. Disconnect, profile replacement,
Output OFF, and shutdown use the existing release paths. Direct Bluetooth and the
bundled receiver remain unverified.

### Rollback

Remove `RollerPress` from the enums, decoder mask, transition mapping, editor map,
and normalization. Existing JSON files that saved `RollerPress` would then need
manual removal before loading, so rollback after release requires a migration.
