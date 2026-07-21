# Tap Dance / Tap-Hold Requirements

Status: Tap & Hold slice implemented; Double Tap and Tap + Hold remain planned

Date: 2026-07-17

The current UI direction is defined in
[ADVANCED_BINDINGS_UI_PLAN.md](ADVANCED_BINDINGS_UI_PLAN.md). The first two-slot
feature is named `Tap & Hold`; `Tap Dance` is reserved for the later Double Tap and
Tap + Hold expansion. This document remains the detailed timing and safety source.

## 目的

現在の `TapToggleHoldLayer` は「短押しでTG、長押しまたは他キー併用でMO」へ
挙動が固定されている。これを、単押し側と長押し側へ別々のactionを選べる
Tap Dance / dual-role behaviorへ置き換える。

最重要体験は、選択中の物理キーについて次の設定を一画面で読めることとする。

```text
Tap:  A
Hold: MO(1)
Term: 200 ms
Interrupt: Hold on other key press
```

## 用語と参考モデル

- QMKのTap Danceはtap回数とhold状態に応じて複数actionを解決する機能である。
- VialはTap Dance定義を独立したslotとして作り、`On tap`、`On hold`、
  `On double tap`、`On tap + hold`を設定してからkeymapへ割り当てる。
- QMK/ZMKのTap-Holdには、別キー入力をtap/hold判定へどう使うかという
  interrupt modeがある。

参考:

- <https://get.vial.today/manual/tap-dance.html>
- <https://docs.qmk.fm/features/tap_dance>
- <https://docs.qmk.fm/tap_hold>
- <https://zmk.dev/docs/keymaps/behaviors/hold-tap>

ACK05 EnhancedではUI名を `Tap Dance` とし、その最初の必須機能を
single Tap / single Holdのdual-role設定とする。Vialの四つのslotを保存できる
モデルを前提にするが、実装はTap/Holdを先に完成させる。

## 対象ユーザーと最短操作

対象はACK05を左手デバイスとして使い、ひとつの物理キーへ通常入力と
layer shiftを重ねたいユーザーである。

最短操作:

1. keymap上でK1-K10のいずれかを選ぶ。
2. Actionから `Tap Dance` を選ぶ。
3. `Create New...` を押す。
4. Tap actionとHold actionを同じ画面で設定する。
5. `Apply and Assign` で定義作成と選択キーへの割当を同時に行う。

Vialと同じくTap Dance定義一覧から先に作る操作も残す。定義は複数キーや
複数レイヤーから再利用できる。

## 機能要件

### 定義と割当

- profileごとに再利用可能なTap Dance定義を持つ。
- 定義は安定したIDと任意の表示名を持つ。例: `td0` / `A · Layer 1`。
- K1-K10のbindingは定義IDを参照する。
- roller左右はUp eventを持たないためTap Danceを割り当てられない。
- Tap Danceから別のTap Danceを参照する再帰設定は禁止する。
- 参照中の定義は削除できない。参照元を一覧表示する。

### 必須slot

- `On Tap`: threshold未満でreleaseされ、Holdへ確定していない場合のaction。
- `On Hold`: threshold超過またはinterrupt ruleでHoldへ確定した場合のaction。
- `Tapping Term`: 100-1000 ms、既定200 ms。150 / 200 / 250 / 300 msを
  presetとして選べる。
- `Interrupt Mode`: 次の3種を定義ごとに選べる。
  - `Hold on other key press`: 別controlのDownで即Hold。layer shift向けの既定。
  - `Hold on other key tap`: 別controlのDown+UpでHold。roll誤判定を減らす。
  - `Timeout only`: threshold超過だけでHold。Tapを優先する。

### 拡張slot

- `On Double Tap`: term内の2回tap。
- `On Tap + Hold`: 1回tap後、term内の2回目を保持。
- 拡張slotが未設定ならsingle Tapはrelease時に即実行し、不要な待ち時間を
  入れない。
- 拡張slotを設定した場合だけ、次のtapを待つためsingle Tap確定をtermまで遅延する。
- triple tap以上、tap回数ごとの任意配列、script callbackは初期範囲外とする。

### action能力

最初の実装で許可する組み合わせ:

| Slot | 許可するaction |
|---|---|
| Tap / Double Tap | Disabled、Keyboard Chord、Mouse Wheel、TG(n) |
| Hold / Tap + Hold | Disabled、Keyboard Chord、MO(n) |

HoldのKeyboard ChordとMOはHold確定時にDown/activateし、物理キーUpで必ず
Up/deactivateする。将来 `TO`、`OSL`、`LT`、macroなどを追加するときは、actionに
Pulse対応／Hold対応の能力を持たせ、slotごとにvalidationする。

## 判定意味論

1. Tap Dance keyのDownでpending stateを作る。Tap actionはまだ出力しない。
2. term超過またはinterrupt rule成立でHoldを確定し、Hold actionを開始する。
3. Hold確定前にreleaseされたらTap候補とする。
4. double slotがなければrelease時にTap actionを実行する。
5. double slotがあればterm内の次回Downを待ってSingle/Double/Tap+Holdを確定する。
6. 一度確定したHold actionは、同じ物理キーのUpまで同じactionへreleaseを返す。
7. disconnect、Stop Live、profile/config変更、例外、終了ではpending danceを破棄し、
   開始済みHold actionをpanic releaseする。未確定Tap actionは実行しない。

別キーの入力をHold判定に使う場合、Hold actionの開始と別キーのroutingを同じ
順序で確定し、layer shiftが別キーより先に有効になることを保証する。

## UI要件

### Keymap画面

- Action paletteに `Tap Dance` categoryを追加する。
- 選択中bindingは `TD0: Tap A / Hold MO(1)` のように要約表示する。
- `Create New...`、`Edit Definition...`、既存定義選択を同じeditorから行える。
- `Apply and Assign` は定義を作成／更新した後、選択キーへ割り当てる。
- 既存定義の編集が複数bindingへ影響する場合は参照数を表示する。

### Tap Dance画面

- Vialと同様に、keymapとは別の `Tap Dance` tabで定義一覧を管理する。
- 右側へTap、Hold、Double Tap、Tap + Holdのcardを並べる。
- 初期表示ではTap/Holdを主役にし、advanced slotは折りたたむ。
- 各cardは現在actionの要約と `Change...` を持つ。
- termは数値入力とpreset、interrupt modeは説明付き選択肢にする。
- 保存前に `Tap A · Hold MO(1) · 200 ms · Hold on press` の一行previewを出す。
- nested modalを深くせず、action選択は共通paletteを使う。

## 保存形式と互換性

- 新しいcomposite actionにはschema v3が必要になる見込みだが、正式contractは
  実装前に `CONFIG_FORMAT.md` で確定する。
- schema v2は引き続き読み込めること。
- 既存 `tapToggleHoldLayer` はsilent conversionせず、従来意味論のlegacy action
  として保持する。
- UIはlegacy actionを `Tap: TG(n) / Hold: MO(n)` の候補へ変換するpreviewを
  提示できる。変換はユーザーが明示的に確定した場合だけ行う。
- 未知field保持、atomic save、`.bak`、validation-before-writeを維持する。

想定model（非確定）:

```json
{
  "tapDances": [
    {
      "id": "td0",
      "name": "A / Layer 1",
      "tappingTermMs": 200,
      "interruptMode": "holdOnOtherKeyPress",
      "onTap": { "kind": "keyboardChord", "keys": ["a"] },
      "onHold": { "kind": "momentaryLayer", "targetLayer": 1 }
    }
  ]
}
```

## Error、Undo、編集中state

- 未設定slot、参照切れ、再帰参照、slot能力に合わないactionは具体的に表示する。
- invalid definitionはDone、Save、Live反映を拒否する。未完成の複合定義はdefinition
  editor内に留め、global draftへ入れない。
- definition editorには `Done` と `Cancel` を残す。Done後の変更はglobal Undo/Redoへ
  1 transactionとして追加する。
- Save後も同じ起動sessionのUndo/Redo履歴を保持する。Save境界を越えてUndoすると
  draftは再びdirtyになる。アプリ終了、Open、New、別configへの切替で履歴を破棄する。
- 最後にSaveしたJSONとは別にworking draftをLocal App Dataへ自動保存し、異常終了後に
  Restore Draft / Discardを提示する。
- Live中の定義変更は保持出力、pending dance、active layerをrelease/resetしてから
  即時再適用する。Saveはfile writeだけを担当する。

## 初期実装でやらないこと

- triple tap以上、任意tap count、script callback
- Tap Danceの入れ子、macro editor、外部API action
- roller方向へのTap Dance
- firmware/Vial protocol互換そのもの
- 全Tap-Hold詳細optionの同時実装。まず3 interrupt modeへ限定する。

## 受け入れ条件

1. K2へ `Tap=A / Hold=MO(1)` を設定し、短押しはA、K2保持中の別キーは
   Layer 1 actionになる。
2. Hold確定後にK2を離すとLayer 1が解除され、TapのAは出力されない。
3. `Tap=TG(1) / Hold=MO(1)` で短押しtoggleと長押しmomentaryを区別できる。
4. 3種類のinterrupt modeを同じ物理sequenceで比較できる。
5. 同じ定義を複数キーへ割り当て、編集時に影響範囲が表示される。
6. Stop Live、disconnect、profile/config変更でHold actionが固着しない。
7. schema v2とlegacy `tapToggleHoldLayer`の読み込み結果が変わらない。

## 実装順

1. Action capabilityとTap Dance定義modelをCoreへ追加する。
2. Tap/Hold resolver、timer、interrupt mode、panic releaseを実装する。
3. schema v3 contractとv2 load compatibilityを確定する。
4. Tap Dance tab、quick create、共通action paletteを実装する。
5. deterministic testと実機Tap/Hold testを通す。
6. 同じmodelへDouble Tap / Tap + Holdを追加する。
