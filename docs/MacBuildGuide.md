# macOS 版のビルドと Mac App Store への提出

macOS 版（`TetherKeeper.Mac`）は、メニューバーに常駐するアプリです。画面は AppKit で作り、キープアライブの処理は Windows 版と同じ `KeepAlive.Core` / `KeepAlive.Infrastructure` を使います。
方針を決めた理由は、[設計判断.md](設計判断.md) の「macOS 版を AppKit のメニューバー常駐アプリとして別に作る」を参照してください。

> macOS 版は作り始めたばかりの「最小の版」です。Windows では macOS 版をビルドできないため、コードは Mac で一度もビルドしていません。
> 最初に Mac でビルドし、下の「最初に確かめること」を順に確かめてください。

## 最小の版に入っているもの

| 機能 | macOS での作り |
| --- | --- |
| メニューバーのアイコン | `NSStatusItem`。動作中・停止中・失敗が続いているときで、アイコン（SF Symbols）が変わる |
| メニュー | 状態、通信方式と送信先、最後の結果、開始・停止、今すぐ実行、設定、ログのフォルダを開く、このアプリについて、終了 |
| 設定のウィンドウ | 言語、通信方式、予備の通信方式、送信先、実行間隔、タイムアウト、通知、起動時の自動開始、ログイン時の起動 |
| 失敗と復旧の通知 | `UNUserNotificationCenter`（通知センター） |
| ログイン時の起動 | `SMAppService.MainApp`（システム設定の「一般」→「ログイン項目」に出る） |
| ログと履歴 | Windows 版と同じ形式で、日ごとのファイルに書く |
| 言語 | 英語と日本語。既定は macOS の言語（システム設定の「言語と地域」のアプリごとの言語を含む）に合わせる |

まだ無いもの（Windows 版にはある）: スリープからの復帰後の再実行、スリープの抑止、ネットワークに応じた開始・停止、自動停止（日時・毎週・通信量の上限）、履歴の画面、ログの画面。

## 必要なもの

- Mac（macOS 13 Ventura 以降。ログイン項目の登録に使う `SMAppService` が macOS 13 からのため）
- .NET 10 SDK
- Xcode（.NET の macos ワークロードが求めるバージョン。`dotnet build` のエラーに表示されます）
- .NET の macos ワークロード

```sh
sudo dotnet workload install macos
```

## ビルドと実行

```sh
# アプリを作って起動する (メニューバーにアイコンが出る。Dock には出ない)
dotnet build TetherKeeper.Mac/TetherKeeper.Mac.csproj
open TetherKeeper.Mac/bin/Debug/net10.0-macos/osx-arm64/TetherKeeper.app

# 単体テスト (Windows 専用のテストは、Mac では何もせずに成功する)
dotnet test KeepAlive.Tests
```

Intel の Mac では、`osx-arm64` を `osx-x64` に読み替えてください。
Visual Studio Code では、C# Dev Kit で `TetherKeeper.Mac.slnx` を開きます（`TetherKeeper.slnx` は Windows 版のソリューションです）。

設定・ログ・履歴は、サンドボックスのコンテナの中に置きます。

```text
~/Library/Containers/com.systemazmax.tetherkeeper/Data/Library/Application Support/TetherKeeper/
├─ settings.json
├─ data-usage.json
├─ logs/TetherKeeper_yyyyMMdd.log
└─ history/TetherKeeper_yyyyMMdd.csv
```

メニューの「ログのフォルダを開く」で、`logs` を Finder で開けます。

## 最初に確かめること

| # | 確かめること | うまくいかないとき |
| --- | --- | --- |
| 1 | ビルドが通るか | AppKit などの API の名前を間違えている可能性があります。エラーを教えてください |
| 2 | メニューバーにアイコンが出て、メニューが開くか | |
| 3 | 「キープアライブを開始」で、Ping（1.1.1.1）が成功するか（ログと、メニューの「最後の結果」） | サンドボックスの中で ICMP が使えない可能性があります（[設計判断.md](設計判断.md) の「1. Ping の実行要件」）。HTTP HEAD と TCP 接続も試し、結果を教えてください |
| 4 | 設定のウィンドウで値を変えて保存でき、⌘C・⌘V が入力欄で使えるか | |
| 5 | 送信先を存在しないアドレス（例: `10.255.255.1`）にして、失敗が続いたときに通知が出るか。戻したときに「復旧しました」の通知が出るか | 初回は macOS が通知の許可を求めます |
| 6 | 「Mac にログインしたときにアプリを起動する」をオンにすると、システム設定の「ログイン項目」に出るか | アプリを「アプリケーション」フォルダに入れてから試してください |
| 7 | 言語を「English」「日本語」に変えて、次の起動で切り替わるか | |

## Mac App Store への提出

### 準備（初回だけ）

1. Apple Developer Program に登録します（年 99 ドル）。
2. [Certificates, Identifiers & Profiles](https://developer.apple.com/account/resources/) で、次を作ります。
   - App ID: `com.systemazmax.tetherkeeper`（`TetherKeeper.Mac.csproj` の `ApplicationId` と合わせます）
   - 証明書: 「Mac App Distribution」（アプリの署名用）と「Mac Installer Distribution」（.pkg の署名用）。作ったら Mac のキーチェーンに入れます
   - プロビジョニング プロファイル: 「Mac App Store Connect」の種類で、上の App ID と証明書を選びます
3. [App Store Connect](https://appstoreconnect.apple.com/) で、新しい macOS アプリを作ります（名前、主言語、バンドル ID、SKU）。
4. アプリのアイコンを用意します（まだありません）。1024×1024 を含むアイコンを `TetherKeeper.Mac/Assets.xcassets/AppIcon.appiconset` に置き、`TetherKeeper.Mac.csproj` に `<AppIcon>AppIcon</AppIcon>` を足します。

### 提出するパッケージを作る

Release でビルドすると、Intel と Apple シリコンの両方で動くアプリを署名し、インストーラー（.pkg）を作ります。
署名に使う証明書とプロファイルの名前は、キーチェーンと Apple Developer のサイトに表示されるものを入れます。

```sh
dotnet publish TetherKeeper.Mac/TetherKeeper.Mac.csproj -c Release \
  -p:CodesignKey="3rd Party Mac Developer Application: <名前> (<チーム ID>)" \
  -p:PackageSigningKey="3rd Party Mac Developer Installer: <名前> (<チーム ID>)" \
  -p:CodesignProvision="<プロビジョニング プロファイルの名前>"
```

できた .pkg（`TetherKeeper.Mac/bin/Release/net10.0-macos/publish/`）を、Mac App Store の「Transporter」アプリで App Store Connect にアップロードします。

### App Store Connect に入れる内容

説明やキーワードは、Microsoft Store の内容（[StoreListing.md](StoreListing.md)）を元にし、「Windows」を「Mac」に、「タスクトレイ」を「メニューバー」に読み替えます。

| 項目 | 入れるもの |
| --- | --- |
| カテゴリ | ユーティリティ |
| プライバシーポリシーの URL | `https://github.com/SystemAzmax/TetherKeeper/blob/master/docs/PrivacyPolicy.md`（macOS 版のことも書き足してから） |
| サポート URL | `https://github.com/SystemAzmax/TetherKeeper/issues` |
| App のプライバシー | 「データを収集していません」 |
| 輸出コンプライアンス | 聞かれません（`Info.plist` の `ITSAppUsesNonExemptEncryption` を `false` にしています） |
| 審査へのメモ | アカウントは不要。メニューバーのアイコンから「キープアライブを開始」を選ぶと、30 秒ごとに 1.1.1.1 へ Ping を送り、メニューの「最後の結果」に結果が出る、と英語で書きます |

## バージョン

`TetherKeeper.Mac.csproj` の `ApplicationDisplayVersion`（表示するバージョン）を、Windows 版（`TetherKeeper.csproj`）と同じ番号にします。
`ApplicationVersion`（ビルド番号）は、App Store Connect にアップロードするたびに 1 つ上げます（同じバージョンでも、ビルド番号が同じものは再アップロードできないため）。
