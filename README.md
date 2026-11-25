# ACK05 Enhanced

The Custom fork includes a separate read-only Windows BLE diagnostic. Its
[hardware findings and usage](docs/research/ACK05_BLE_WINDOWS.md) cover direct
Bluetooth discovery, GATT service access and the remaining capture tests.

[![Windows build](https://github.com/46slv/ACK05-Enhanced/actions/workflows/windows-build.yml/badge.svg)](https://github.com/46slv/ACK05-Enhanced/actions/workflows/windows-build.yml)

The current schema-v4 preview includes reusable Radial Menus: hold an assignable
input to open a focusless wheel near the pointer, navigate with two inputs (such
as Roller CW/CCW), and release to run one safe action. The wheel uses external
labels, a compact center, and active-monitor edge clamping.

XPPen ACK05 Shortcut Remoteを、Vial / QMKのようなレイヤー付きキーマップで
使うためのWindows companionアプリです。物理キーを見ながら、通常キー、
`MO(n)` / `TG(n)`、Tap & Hold、2キーCombo、ホイール出力、アプリ・ファイル・
フォルダー・URLの起動を編集できます。

> **Preview:** 現在はソースビルド向けです。配布パッケージ、コード署名、
> USB receiver対応、direct Bluetooth対応はまだ正式リリース品質ではありません。

ACK05 EnhancedはXP-Pen公式製品ではない独立したオープンソースプロジェクトです。
firmware、pairing、ドライバー、HidHide設定を自動変更しません。

## 主な機能

- ACK05の実機配置に沿ったK1-K10／ローラーのキーマップ編集
- profileごとに2-8レイヤー（新規設定は4レイヤー）
- Transparent、keyboard chord、mouse wheel、Disabled
- 1キーでアプリ、ファイル、フォルダー、ショートカット、URLを開くOpen Target
- 任意のK1-K10へ割り当てられる `MO(n)` と `TG(n)`
- `Tap=B / Hold=Shift`、`Tap=B / Hold=MO(1)` の再利用可能なTap & Hold
- exactly-two-key Combo（例: `K1 + K2 = Enter`）と判定パラメーター
- foreground processに応じたprofile切り替え
- 一押し即時反映、ドラッグ＆ドロップ、Undo / Redo、Revert to Saved
- 明示Saveとは別のworking-draft復旧
- Output OFFを初期値とする安全な実機出力境界

Layer 0で `K2=A`、`K1=MO(1)`、Layer 1で `K2=B` と設定すると、
K2単独はA、K1を保持しながらK2を押すとBになります。K1は予約キーではなく、
K2-K10と同様に自由に編集できます。

## 対応環境

- Windows 10 / 11 x64
- .NET 8 Desktop Runtime（ビルドには.NET 8 SDK）
- 確認済みデバイス: XPPen ACK05 `VID 28BD / PID 0202`

Windowsから `FF0A:0001` vendor collectionを取得できる接続では、K1-K10、
ローラー、出力安全性を実機確認しています。USB receiverは手元にないため未確認です。
direct Bluetoothは所有者環境で操作テストを通過した一方、別のread-only enumerationでは
vendor collectionを再現できていません。そのため現段階ではexperimental扱いで、環境を
またいだ正式対応は保証しません。

## ビルドと起動

```powershell
git clone https://github.com/46slv/ACK05-Enhanced.git
cd ACK05-Enhanced
dotnet build .\Ack05Enhanced.sln -c Release
dotnet run --project .\tests\Ack05Enhanced.SelfTest -c Release
dotnet run --project .\src\Ack05Enhanced.App -c Release
```

アプリはOutput OFFで起動します。設定を編集・保存するだけではACK05へコマンドを
送らず、Windows入力も生成しません。実機出力はヘッダーの `Output OFF` を押し、
警告を確認して明示的に有効化します。

## 基本操作

1. profileとLayer 0-7を選びます。
2. 中央のK1-K10またはローラー方向を選びます。
3. 下のAction Paletteからactionを一度押すとworking draftへ即時反映されます。
4. Tap & HoldとCombosは専用workspaceで定義し、物理キーへ割り当てます。
5. Undo / Redoで編集履歴を移動し、Revertで最後の明示Saveへ戻します。
6. `Save` でJSONへ永続化します。Output ON中のvalidな変更はSave前でも反映されます。

Action Paletteの境界は上下へドラッグでき、ウィンドウの高さに合わせて選択肢を
広く表示できます。狭い幅や高DPI環境では、4つのworkspaceタブが一つの
workspace selectorへ切り替わります。

詳しい操作は [USER_GUIDE.md](USER_GUIDE.md)、導入手順は
[INSTALL.md](INSTALL.md) を参照してください。

## 安全設計

- device enumerationとdecodeはread-onlyが既定
- raw modeは文書化済みコマンドだけを明示opt-inで送信
- SendInputはGUIのOutput ONまたはCLIの明示flagが必要
- Open TargetはGUIのOutput ONが必要。CLIでは別の明示許可flagも必要
- disconnect、Output OFF、profile/config変更、終了時に保持キーをpanic release
- private captureと個人configはGit管理外
- driver、HidHide、firmware、pairing、XPPen driverの制御は対象外

詳細は [SECURITY.md](SECURITY.md) と
[UNDO_AND_SAFETY.md](UNDO_AND_SAFETY.md) を参照してください。

## Probe CLI

read-only enumerationとmonitoring:

```powershell
dotnet run --project .\src\Ack05Enhanced.Probe -- list
dotnet run --project .\src\Ack05Enhanced.Probe -- monitor
dotnet run --project .\src\Ack05Enhanced.Probe -- raw-input --list
```

documented raw-mode commandやSendInputを使うコマンドには明示flagが必要です。

```powershell
dotnet run --project .\src\Ack05Enhanced.Probe -- monitor --enable-bitmask
dotnet run --project .\src\Ack05Enhanced.Probe -- run `
  --config .\examples\hardware-output-smoke.json `
  --enable-bitmask --allow-sendinput
```

## プロジェクト構成

```text
src/
  Ack05Enhanced.Core/      OS非依存のdecode、layer routing、config、action
  Ack05Enhanced.Windows/   HID transport、SendInput、runtime
  Ack05Enhanced.App/       Windows Forms keymap editor
  Ack05Enhanced.Probe/     HID / raw report診断CLI
tests/
  Ack05Enhanced.SelfTest/  hardware不要のdeterministic self-test
docs/
  design/                  UI・将来機能の設計メモ
  research/                根拠付きprotocol調査
examples/                  共有用の安全な設定例
```

設定形式は [CONFIG_FORMAT.md](CONFIG_FORMAT.md)、正式仕様は
[SPEC.md](SPEC.md)、検証手順と実機結果は [TEST_PLAN.md](TEST_PLAN.md)、
現在の優先順は [ROADMAP.md](ROADMAP.md) にあります。

## 開発に参加する

IssueやPull Requestを歓迎します。変更前に [CONTRIBUTING.md](CONTRIBUTING.md) の安全境界を読み、
CoreをWindows APIから独立させ、held synthetic keyのmatching key-upを必ず維持してください。
protocol、transport、layer semantics、output behaviorを変更する場合は、関連する仕様・
テスト・判断記録も同じ変更で更新してください。

Pull Request前の最小確認:

```powershell
dotnet build .\Ack05Enhanced.sln -c Release
dotnet run --project .\tests\Ack05Enhanced.SelfTest -c Release
```

private HID capture、device path、個人用process名をIssueやGitへ含めないでください。
脆弱性や安全上の問題は公開Issueへ詳細を貼らず、[SECURITY.md](SECURITY.md) の方針に
従ってください。

## ライセンス

[MIT License](LICENSE) で公開します。
