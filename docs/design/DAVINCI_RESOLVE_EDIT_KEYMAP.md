# DaVinci Resolve Edit Page Keymap

Status: Accepted design baseline / not yet implemented or hardware-tested

Date: 2026-07-20

Target: DaVinci Resolve 20, Edit page, Windows 10/11, ACK05 wired USB

## 1. Goal

ACK05を左手用の小型編集コンソールとして使い、Editページで素材確認、配置、
トリム、削除、再生確認までをマウスなしで繰り返せるようにする。

この文書をDaVinci Resolve Editページ用プロファイルの実装・評価における基準とする。
実機テストで変更する場合は、変更理由と結果をこの文書および`DECISIONS.md`へ記録する。

Initial configuration artifact:

- `examples/davinci-resolve-edit.json`
- `examples/DAVINCI_RESOLVE_EDIT_SETUP.md`

Speed Editorをそのまま縮小するのではなく、ACK05の物理形状に合わせて次の3要素を
中心にする。

- K1-K6: 方向を身体で覚えられるナビゲーションパッド
- K7/K8/K10: 操作面を一時的に切り替えるレイヤーキー
- K9: どの状態からでも押せる大きなPlay/Stopキー

## 2. Fixed physical grammar

```text
┌──────────┐  ┌────┬────┬────┬────────┐
│          │  │ K1 │ ↑2 │ K3 │ K7 Trim│
│  ROLLER  │  ├────┼────┼────┤        │
│          │  │ ←4 │ ↓5 │ →6 │        │
└──────────┘  ├────┼─────────┼────────┤
              │ K8 │ K9 Space│ K10    │
              │Asm │         │Utility │
              └────┴─────────┴────────┘
```

全レイヤーで次の位置関係を維持する。

- K2 = 上／前の対象
- K4 = 左／戻す／前フレーム
- K5 = 下／次の対象
- K6 = 右／進める／次フレーム
- K1/K3 = 左右一対のコマンド
- K9 = Space（Play/Stop）

K9はLayer 0だけでなく全レイヤーへ明示的にSpaceを割り当てる。Transparentにはせず、
将来レイヤーを増やしても再生停止が変わらない安全キーとして固定する。

## 3. Assumptions

- DaVinci Resolve標準キーボードプリセットを基準にする。
- Resolve側のKeyboard Customizationで専用ショートカットを追加できる。
- K7/K8/K10はDown/Upを使えるため、押している間だけ有効な`MO`にする。
- レイヤーのラッチや`TG`は使わない。
- K7/K8/K10同士だけを2キーCombo候補にする。
- 初期版はEditページのみ。Cut、Fusion、Color、Fairlight、Deliverは対象外。
- クリップのドラッグ、複雑なトラックパッチ、Inspectorの数値編集はv1対象外。

## 4. Layer 0: Navigate

通常面。タイムライン移動、フレーム送り、IN/OUT、再生を担当する。

```text
ROLLER  Jog 1 frame backward / forward

K1  Mark In
K2  Previous edit point                 ↑
K3  Mark Out
K4  Step one frame backward             ←
K5  Next edit point                     ↓
K6  Step one frame forward              →
K7  Hold Trim layer
K8  Hold Assemble layer
K9  Play/Stop                           Space
K10 Hold Utility layer
```

K4/K6は必ず1フレーム移動、ローラーも初期版では1ノッチ1フレームとする。
重複しているが、K4/K6は確実な単発入力、ローラーは連続操作という触感の違いがある。

## 5. Layer 1: Trim — hold K7

K7を押している間だけ有効。方向パッドの意味を編集点操作へ変える。

```text
ROLLER  Trim selected edit 1 frame left / right

K1  Trim start to playhead
K2  Select previous / nearest edit       ↑
K3  Trim end to playhead
K4  Trim selected edit left 1 frame      ←
K5  Cycle selected edit side/type        ↓
K6  Trim selected edit right 1 frame     →
K7  Hold Trim layer
K8  Insert edit
K9  Play/Stop                            Space
K10 Ripple delete selected clip/edit
```

### Trim loop

1. K7を保持する。
2. K2で編集点を選ぶ。
3. K5でOutgoing / Incoming / Rollを切り替える。
4. K4/K6またはローラーでフレーム単位に調整する。
5. K9で再生確認する。
6. K7を離して通常面へ戻る。

K1/K3は「開始／終了」、K4/K6は「左／右」とし、左右一対の規則を崩さない。

## 6. Layer 2: Assemble — hold K8

K8を押している間だけ有効。SourceとTimelineを往復し、素材を配置する。

```text
ROLLER  Source/Timeline jog 1 frame backward / forward

K1  Mark In
K2  Previous clip/edit                   ↑
K3  Mark Out
K4  Source/Timeline viewer toggle        ←
K5  Select nearest edit / focus timeline ↓
K6  Match Frame                          →
K7  Insert edit
K8  Hold Assemble layer
K9  Play/Stop                            Space
K10 Overwrite edit
```

初期版ではK8を保持してK7でInsert、K10でOverwriteとする。K7+K8 Comboは
レイヤー操作とのタイミング競合を避けるため無効にする。

### Source-to-timeline loop

1. K8を保持してK4でSource/Timelineを切り替える。
2. ローラーまたはK4/K6で素材位置を探す。
3. K8を離し、K1/K3でIN/OUTを設定する。
4. K8を保持したままK7でInsert、K10でOverwriteする。

## 7. Layer 3: Utility — hold K10

K10を押している間だけ有効。ツール、選択、削除、保存を担当する。

```text
ROLLER  Timeline zoom out / in

K1  Delete and leave gap
K2  Selection Mode                       ↑
K3  Ripple Delete
K4  Undo                                 ←
K5  Blade Edit Mode                      ↓
K6  Redo                                 →
K7  Toggle Snapping
K8  Save Project
K9  Play/Stop                            Space
K10 Hold Utility layer
```

削除とBladeをUtility面へ隔離し、通常操作中の誤爆を減らす。Undo/RedoはK4/K6へ
置き、履歴上の左右移動として覚える。

## 8. Future layer-key combos

K7/K8/K10同士のComboは将来候補とし、初期版ではすべてDisabledにする。

| Combo | Action | Reason |
|---|---|---|
| K7 + K8 | Insert Edit | 将来候補 / 初期版Disabled |
| K8 + K10 | Source/Timeline Viewer Toggle | 将来候補 / 初期版Disabled |
| K7 + K10 | Save Project | 将来候補 / 初期版Disabled |

### Why disabled initially

現行Combo resolverは候補キーを認識時間だけbufferする。レイヤーキーをComboの
メンバーにもすると、単独MOの開始が遅れるうえ、`K8を保持してK10=Overwrite`と
`K8+K10=Viewer Toggle`が押下間隔で変化する。初期版は操作結果を予測できることを
優先し、K7/K8/K10をMO専用にする。

将来有効化する場合は、Combo scopeをLayer 0だけに限定し、20-25 msで実機テストする。
Combo不成立時のreplay、単独MOの遅延、意図しないCombo成立を各20回以上確認する。

## 9. Initial implementation map

実装時の曖昧さをなくすため、初回は次を正式案とする。

- K7 = MO(Trim)
- K8 = MO(Assemble)
- K10 = MO(Utility)
- K8 layer: K7 = Insert、K10 = Overwrite
- K10 layer: K8 = Save Project
- K7/K8/K10相互のCombo = Disabled until hardware timing is evaluated
- K9 = Space on every layer

## 10. Resolve shortcut setup

既定キーが明確な操作はそのまま利用する。次の意味コマンドはResolveの
`Keyboard Customization`で現在の割当を確認し、未割当または競合する場合は
ACK05専用の競合しにくいキーコードへ割り当てる。

| Semantic command | Resolve command to search | ACK05 position |
|---|---|---|
| Previous / Next edit | Go to Previous/Next Edit | L0 K2 / K5 |
| Step one frame | Step One Frame Reverse/Forward | L0 K4 / K6, Roller |
| Trim start/end to playhead | Trim Start/End | Trim K1 / K3 |
| Select/cycle edit side | Select Nearest Edit / Cycle Edit Point | Trim K2 / K5 |
| Trim one frame | Nudge/Trim Left/Right One Frame | Trim K4 / K6, Roller |
| Insert / Overwrite | Insert / Overwrite | Assemble K7 / K10 |
| Viewer toggle | Source/Timeline Viewer | Assemble K4 |
| Delete gap / Ripple delete | Delete Selected / Ripple Delete | Utility K1 / K3 |
| Timeline zoom | Zoom Out/In | Utility Roller |

専用割当は`Ctrl+Alt+Shift+F1`のような通常編集で押さない組み合わせから始める。
Resolveが受け付けること、Windowsや常駐アプリと競合しないことをKeyboard
Customization画面で確認する。確認前に「既定キー」として固定しない。

最終的にはACK05設定JSONとResolveキーボードプリセットを一組として管理する。

## 11. Safety behavior

- K7/K8/K10のMOはキーUp、切断、プロファイル変更、Live停止、終了時に解除する。
- レイヤーキーを離した後に状態が残るtoggle layerは使わない。
- K9 Spaceは全レイヤーで同一出力にする。
- ACK05切断時は保持中のSpace、修飾キー、レイヤーをすべてreleaseする。
- Resolve以外がforegroundのときはこのプロファイルを選ばない。
- Combo timingがレイヤー操作を阻害する場合、Comboを先に減らす。
- 削除系はUtility、Ripple trimはTrimへ隔離する。

## 12. v1 non-goals

- Speed EditorのSearch Dialモード切替の完全再現
- Source Tape、Sync Bin、Multicam、Camera番号選択
- トラック追加、ロック、Auto Track Selectorの全操作
- Inspector、Effects Library、Media Pool内の完全キーボードナビゲーション
- Fairlight、Color、Deliverの操作
- マクロ、スクリプト、外部APIによるResolve制御

## 13. Validation plan

1. Resolveでキーボードプリセット`ACK05 Edit`を作る。
2. 意味コマンドを検索し、実際の割当を記録する。
3. `Resolve.exe`専用プロファイルと4レイヤーを作る。
4. K9が全レイヤーでSpaceになることをself-testする。
5. K7/K8/K10の各MOを単独で20回操作し、立ち上がり遅延と取りこぼしを測る。
6. K7/K8/K10同士の同時押しで意図しないComboが発火しないことを確認する。
7. 5クリップの複製タイムラインで配置、移動、trim、delete、undoを確認する。
8. レイヤー保持中の抜線、Live停止、別アプリへの切替でkey-upが残らないことを確認する。
9. 30分編集し、マウスが必要になった操作と誤操作を記録する。

## 14. Evidence boundary

- Blackmagic DesignはEditページのJKL dynamic trimming、professional trimming、
  keyboard shortcut customizationを公式に案内している。
- Speed Editor / Editor Keyboardページは検索ダイヤル、Trim In/Out、編集点選択を
  専用操作として説明している。
- Windows既定キーはResolveの版、選択中パネル、Keyboard Presetで変わり得るため、
  この文書では意味コマンドを正式な設計境界とする。

Official references:

- https://www.blackmagicdesign.com/products/davinciresolve/edit
- https://www.blackmagicdesign.com/products/davinciresolve/keyboard
- https://documents.blackmagicdesign.com/UserManuals/DaVinci-Resolve-20-Editors-Guide.pdf
