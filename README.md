# TetherKeeper（KeepAlive for Tethering）

iPhone のインターネット共有（テザリング）、Wi-Fi ルーター、モバイル回線、一部の VPN は、一定時間通信が無いと接続を切断することがあります。
TetherKeeper は定期的に小さな通信を発生させて、このアイドル切断を起きにくくするツールです。

> テザリングの切断を 100% 防げる保証はありません。

## 機能

- 3 つの通信方式（設定画面で切り替え）
  - **Ping (ICMP)**: 通信量が最小で、管理者権限も不要（既定）
  - **HTTP HEAD**: ICMP が遮断された環境やプロキシ環境向け
  - **TCP Connect**: 接続を確立したらすぐに切断する
- 実行間隔（5〜3600 秒）とタイムアウトを設定可能
- 成功・失敗の回数、連続失敗数、ログの表示
- 「今すぐ実行」ボタン
- スリープ復帰を検知すると、5 秒後に即時実行する
- 指定した日時にキープアライブを自動停止（モニター画面で日付と時刻を選んで「予約」。停止後は予約が自動で解除される）
- 通信量の上限での自動停止（設定画面で上限を MB で指定。キープアライブ動作中の通信量を、モニター画面の「リセット」を押すまで累計し、上限に達したら停止する。上限に達している間は開始できない）
  - 数えるのは、インターネットへの通信に使われているネットワークアダプター（有線 LAN と Wi-Fi の両方につながっている場合は Windows が通信に選んだ方）の送受信量で、ほかのアプリの通信も含む。どのアダプターを数えているかはログに「通信量の計測対象」として出る
  - 計測は 5 秒ごとのため、その間の通信の分だけ上限を超えてから止まる。キープアライブを止めても、ほかのアプリの通信は止まらない
- アプリ起動時の自動開始、Windows ログイン時の自動起動（トレイに格納した状態で起動）
- タスクトレイ常駐（× ボタンでトレイに格納。終了は画面の「終了」ボタンかトレイメニューから）
- ログのファイル出力（1 日 1 ファイル、30 日分を保存）

## 動作環境

- Windows 10 1809 以降 / Windows 11
- 開発時: .NET 10 SDK と MAUI ワークロード（`maui-windows`）

## ビルドと実行

```powershell
# Windows 版を起動
dotnet build TetherKeeper/TetherKeeper.csproj -f net10.0-windows10.0.19041.0
dotnet run --project TetherKeeper/TetherKeeper.csproj -f net10.0-windows10.0.19041.0

# 単体テスト
dotnet test KeepAlive.Tests
```

Visual Studio では `TetherKeeper.slnx` を開き、デバッグ対象に「Windows Machine」を選びます。

設定ファイルは `%LOCALAPPDATA%\TetherKeeper\settings.json` に、通信量の累計は同じフォルダの `data-usage.json` に保存されます。
ログは同じフォルダ内の `logs\TetherKeeper_yyyyMMdd.log` に 1 日 1 ファイルで出力され、30 日を過ぎたファイルは自動で削除されます。
保存フォルダは、モニター画面の「保存先」ボタンで開けます。
（上記は開発時の非パッケージ版の場所です。MSIX 版は下記のとおりパッケージ専用フォルダになります）

## MSIX でのインストール

### パッケージの作成（開発 PC）

```powershell
powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1
```

`artifacts\msix\` に次のファイルが出力されます。

| ファイル | 用途 |
|---|---|
| `TetherKeeper_<バージョン>_Test\TetherKeeper_<バージョン>_x64.msix` | インストーラー本体（.NET と Windows App SDK を同梱しているため単体でインストール可能） |
| `TetherKeeper.cer` | 署名証明書の公開鍵。インストール先 PC で信頼登録に使う |

署名用の自己署名証明書（`CN=TetherKeeper`、有効期間 5 年）は、初回実行時に現在のユーザーの証明書ストアに自動で作成されます。

署名にはタイムスタンプを付けるため、実行時にインターネット接続が必要です（タイムスタンプサーバーに送られるのは署名のハッシュ値だけです）。使うサーバーは `-TimestampUrl` で変更できます。

### インストール先 PC での作業

1. **証明書の信頼登録（PC ごとに最初の 1 回だけ。管理者権限が必要）**

   管理者の PowerShell で次を実行します。

   ```powershell
   Import-Certificate -FilePath TetherKeeper.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   画面操作で行う場合は、`TetherKeeper.cer` をダブルクリック →「証明書のインストール」→ 保存場所「ローカル コンピューター」→「証明書をすべて次のストアに配置する」で「信頼されたユーザー」を選びます。

2. **インストール（管理者権限は不要。使う本人のアカウントで実行）**

   `.msix` をダブルクリックして「インストール」を押します。スタートメニューに TetherKeeper が追加されます。

### 更新

`TetherKeeper\TetherKeeper.csproj` の `ApplicationVersion` を 1 つ上げてから `Publish-Msix.ps1` を実行し、新しい `.msix` をダブルクリックします。設定とログは引き継がれます（同じバージョンのままだと更新できません）。

### アンインストール

「設定」→「アプリ」→「インストールされているアプリ」から TetherKeeper をアンインストールします。設定とログもパッケージ専用フォルダごと削除されます。

### 非パッケージ版との違い

| 項目 | 非パッケージ版（開発時の exe 直接実行） | MSIX 版 |
|---|---|---|
| 設定・ログの保存先 | `%LOCALAPPDATA%\TetherKeeper` | `%LOCALAPPDATA%\Packages\TetherKeeper_<発行元ID>\LocalState`（「保存先」ボタンで開ける） |
| ログイン時の自動起動 | レジストリの Run キー | StartupTask（タスクマネージャーの「スタートアップ アプリ」でも切り替え可能） |
| アンインストール時のデータ | 残る | 自動で削除される |

タスクマネージャーで自動起動を無効にした場合、Windows の仕様によりアプリの設定画面からは再び有効にできません。タスクマネージャーの「スタートアップ アプリ」で有効に戻してください。

### 署名証明書の管理

署名に使う証明書（秘密鍵付き）は、パッケージを作成した PC の「現在のユーザー」の証明書ストアにだけあります。PC の入れ替えなどで失った場合は、新しい証明書が自動で作られます。その場合、各 PC で新しい `TetherKeeper.cer` を手順 1 と同じように信頼登録してください。

秘密鍵はエクスポートできない設定で作成されるため、PC の外へ持ち出したりバックアップしたりはできません。これは、秘密鍵が盗まれると、この証明書を信頼したすべての PC で任意のパッケージが警告なしにインストールできてしまうのを防ぐためです。

以前のスクリプトで作った証明書は秘密鍵をエクスポートできるため、実行時に警告が表示されます。次のように実行すると新しい証明書に切り替わります。この場合も、各 PC で新しい `TetherKeeper.cer` の信頼登録が必要です。古い証明書は `certmgr.msc`（現在のユーザー →「個人」→「証明書」）から削除してください。

```powershell
powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1 -NewCertificate
```

## ソリューション構成

```text
├─ KeepAlive.Core            OS 非依存のロジック
│   ├─ Models                AppSettings / KeepAliveResult / LogEntry / KeepAliveStatistics / DataUsage
│   ├─ Interfaces            IKeepAliveStrategy / ISettingsService / ILogService / IKeepAliveWorker / IDataUsageTracker など
│   ├─ Services              KeepAliveWorker / SettingsService / LogService / KeepAliveStrategyResolver / DataUsageTracker
│   ├─ Strategies            Ping / HttpHead / TcpConnect の各 KeepAliveStrategy
│   └─ Configuration         DI 登録（AddKeepAliveCore）と KeepAliveOptions
├─ KeepAlive.Infrastructure  OS 依存の実装
│   ├─ Settings              JsonFileSettingsStore / JsonFileDataUsageStore（通信量の累計）
│   ├─ Network               NetworkInterfaceDataUsageMeter（アダプターの送受信量）
│   ├─ AutoStart             WindowsAutoStartService（HKCU\Run）
│   └─ Power                 WindowsPowerEventSource（SystemEvents）
├─ TetherKeeper              .NET MAUI の UI（Views / ViewModels / Platforms）
├─ KeepAlive.Tests           xUnit の単体テスト
├─ docs/設計判断.md           方式比較・スリープ時の挙動・フレームワーク選定
└─ docs/今後の機能候補.md     今後実装を検討する機能と改善点
```

設計の背景と、指示書との差異（.NET 10 を採用した理由など）は [docs/設計判断.md](docs/設計判断.md) にまとめています。
今後実装を検討する機能は [docs/今後の機能候補.md](docs/今後の機能候補.md) にまとめています。

## 通信方式を追加するには

1. `KeepAliveMethod` に値を追加します。
2. `IKeepAliveStrategy` を実装します。通信の失敗は例外にせず `KeepAliveResult.Failed` で返してください。
3. `AddKeepAliveCore()` に登録します。
4. `SettingsViewModel.Methods` に表示名を追加します。
