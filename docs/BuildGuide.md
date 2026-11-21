# ビルド方法

ビルドと実行、MSIX パッケージの作り方は、[README_ja.md](../README_ja.md) の「ビルドと実行」と「MSIX でのインストール」にあります。
ここでは、リリースのときに確かめる、アプリ名・バージョン・ライセンス表示の扱いをまとめます。

## アプリ名（日本語名と英語名）

アプリ名は、表示言語が日本語の場合は **テザリングキーパー**、それ以外の言語では **TetherKeeper** です。
名前を変えるときは、次の 3 か所をそろえます。

| 表示される場所 | 名前を決めているところ | 言語の選び方 |
| --- | --- | --- |
| スタートメニュー、タスクバー、設定のアプリ一覧、タスク マネージャーの「スタートアップ アプリ」（MSIX 版） | `TetherKeeper\Platforms\Windows\Strings\<言語>\Resources.resw` の `AppDisplayName` | Windows の言語の設定。日本語が無い場合は、既定の言語（en-US）の名前 |
| ウィンドウのタイトル、タスクトレイのツールチップ、「このアプリについて」 | `KeepAlive.Core\Services\AppDisplayName.cs` | 表示言語（`CultureInfo.CurrentUICulture`）が日本語かどうか |
| 日本語の文章の中の名前（メッセージ、ログ、ライセンス表示など） | `AppDisplayName.JAPANESE` と、`tools\notices\build-notices.sh` | 常に日本語名 |

- `Package.appxmanifest` の `DisplayName`・`Description` と、自動起動（`StartupTask`）の `DisplayName` は、
  `ms-resource:AppDisplayName` のようにリソースを参照しています。マニフェストに名前を直接書かないでください。
- パッケージの既定の言語（`TetherKeeper.csproj` の `DefaultLanguage`）は `en-US` です。
  日本語と英語以外の言語では、既定の言語の名前（TetherKeeper）が使われます。
- データの保存先のフォルダー名、パッケージ名、exe の名前、ログのファイル名、多重起動の判定に使う名前などの識別子は、
  英語名のまま変えません。変えると、既存のデータを読めなくなります。
- csproj の `ApplicationTitle` は MAUI の既定の表示名ですが、MSIX 版ではマニフェストのリソースの名前が使われます。

### Partner Center での名前の予約

パッケージに入れた名前は、言語ごとに Partner Center で予約済みの名前である必要があります。
予約していない名前があると、パッケージのアップロードでエラーになります。

1. Partner Center のアプリの **製品管理** → **アプリ名の管理** で、
   `テザリングキーパー` と `TetherKeeper` の両方を予約します。
2. Visual Studio で TetherKeeper のプロジェクトを右クリックし、
   **公開** → **アプリケーションをストアと関連付ける** を選びます（予約を足したときは、やり直します）。
   `Package.appxmanifest` の `Identity` の `Name` と `Publisher` が Partner Center のアプリの値に書き換わり、
   `Package.StoreAssociation.xml` ができます。
   - Visual Studio にこのメニューが無い場合は、Partner Center の **製品管理** → **製品 ID** にある
     `Package/Identity/Name` と `Package/Identity/Publisher` の値を、`Identity` の `Name` と `Publisher` に書き写します。
   - 関連付けのあとに `Package.appxmanifest` の `DisplayName` が書き換わっていたら、
     `ms-resource:AppDisplayName` に戻します。
   - `Publisher` が変わると、`build\Publish-Msix.ps1` の自己署名証明書（`CN=TetherKeeper`）では署名できなくなります。
     ストアに出すパッケージは、Partner Center にアップロードするとストアが署名します。
3. 申請の **ストア登録情報** で、言語ごとの **製品名** を選びます（日本語はテザリングキーパー、英語は TetherKeeper）。
   英語の登録情報を用意しない場合は、**言語の追加と削除** で英語を外せます。

## バージョンの上げ方

バージョンは、`TetherKeeper\TetherKeeper.csproj` の `ApplicationDisplayVersion` の 1 か所で決めます。
MAUI のビルドが、ここから exe 版とストア版（MSIX）の両方のバージョンを作るため、2 つの版はいつも同じバージョンになります。
Microsoft Store で最初に公開するバージョンは `1.0.0` です。

| | 作られるバージョン | `ApplicationDisplayVersion` が `1.0.0` のとき |
| --- | --- | --- |
| exe 版 | アセンブリのバージョン（`ApplicationDisplayVersion`） | `1.0.0.0` |
| ストア版（MSIX） | `Package.appxmanifest` の `Identity` の `Version`（`ApplicationDisplayVersion.ApplicationVersion`） | `1.0.0.0` |

- リリースのときは、`ApplicationDisplayVersion` を上げます（例: `1.0.0` → `1.1.0`）。
- `ApplicationVersion` は `0` のままにします。ストアに提出するパッケージは、4 つ目の数字（リビジョン）を 0 にする必要があるためです。
- `Package.appxmanifest` の `Version="0.0.0.0"` はビルドで置き換えられるので、書き換えません。
- MAUI は、csproj のバージョンを変えただけではマニフェストを作り直しません。
  そのため csproj の `RegenerateAppxManifestWhenVersionChanges` で、バージョンが前回と違うときに作り直させています。
- 画面では、4 つ目の数字が 0 のときは省いて `1.0.0` のように表示します。
- MSIX は、インストール済みのものより大きいバージョンでないと更新できません。
  以前の `Publish-Msix.ps1` で作ったパッケージは `1.0.0.1` のため、それを入れた PC には `1.0.0`（`1.0.0.0`）を上書きでインストールできません。
  いったんアンインストールしてから入れ直してください（設定とログも削除されます）。
  ストアと関連付けたあとのパッケージは名前と発行元が変わり、別のアプリとして扱われるため、この制限は受けません。

## ライセンス表示の更新

「ライセンス情報」ボタンで開く `TetherKeeper\ThirdPartyNotices.txt` は、
アプリと一緒に配布するソフトウェアのライセンスを、原文のまま並べたものです。
NuGet パッケージを追加・更新したときや、.NET MAUI・.NET のランタイム・Windows App SDK を更新したときは、このファイルも更新してください。

ファイルは `tools\notices\build-notices.sh` で作ります。

1. スクリプトに書いてあるパッケージのバージョン（フォルダー名と、冒頭の一覧・各節の見出し）を、
   使っているバージョンに書き換えます。パッケージを追加したときは、節も追加します。
   - 使っているバージョンは、`TetherKeeper\obj\project.assets.json` の
     `net10.0-windows10.0.19041.0/win-x64` で確かめます。
   - 配布物に入る DLL は、`Publish-Msix.ps1` で作った `.msix` の中身で確かめます
     （拡張子を `.zip` に変えると中を見られます）。直接参照していないパッケージ（依存先）も対象です。
2. 一度 `Publish-Msix.ps1` で MSIX を作り、NuGet のキャッシュにパッケージとランタイムを取得しておきます。
3. Git Bash で、リポジトリのフォルダーから次を実行します。
   `TetherKeeper\ThirdPartyNotices.txt` が上書きされます。

   ```bash
   bash tools/notices/build-notices.sh
   ```

各節の取り出し元は次のとおりです。

- 各節の内容は、`%USERPROFILE%\.nuget\packages\<パッケージ名>\<バージョン>\` にある
  `LICENSE.TXT`、`License.md`、`NOTICE.txt`、`THIRD-PARTY-NOTICES.TXT` などから取っています。
  .NET の節は `microsoft.netcore.app.runtime.win-x64` の `LICENSE.TXT` と `THIRD-PARTY-NOTICES.TXT` です。
- Microsoft.Extensions のパッケージにはライセンスファイルが無いため、同じリポジトリ（dotnet/dotnet）から作られている
  .NET のランタイムの `LICENSE.TXT` を載せています。`THIRD-PARTY-NOTICES.TXT` はランタイムと同じ内容です。
- パッケージにライセンスファイルが無い Microsoft.IO.RecyclableMemoryStream は、
  パッケージを作ったコミットのリポジトリ（microsoft/Microsoft.IO.RecyclableMemoryStream）の LICENSE を
  `tools\notices\licenses` に置いて、そこから取っています。
- Win2D、Windows App SDK、Windows SDK の .NET 向けプロジェクションは、パッケージにライセンスの本文が無いため、
  ライセンス条項の名前と URL を載せています。
