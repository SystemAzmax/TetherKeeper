# Microsoft Store の申請内容（1.0.0）

Partner Center で申請するときに入れる内容です。
手順は [StoreReleaseKit の NewAppGuide.md](https://github.com/SystemAzmax/StoreReleaseKit/blob/main/NewAppGuide.md) の「6. Partner Center で申請する」に従い、ここには TetherKeeper で入れる値だけを書きます。
画面の名前は、日本語の表示の後ろに英語の表示を（）で書いています。

## 6.2 プロパティ（Properties）

| 項目 | 入れるもの |
| --- | --- |
| カテゴリ（Category） | ユーティリティ & ツール（Utilities & tools） |
| プライバシー ポリシーの URL | `https://github.com/SystemAzmax/TetherKeeper/blob/master/docs/PrivacyPolicy.md` |
| Web サイト | `https://github.com/SystemAzmax/TetherKeeper/blob/master/docs/UserGuide.md` |
| サポートの連絡先情報 | `https://github.com/SystemAzmax/TetherKeeper/issues` |
| 製品の宣言（Product declarations） | チェックを入れない（アプリ内の購入、クラウドへのバックアップなどは無い） |
| システム要件 | 何も選ばない |

## 6.3 年齢区分（Age ratings）

ゲームでも SNS でもない道具のアプリとして答えます。暴力の表現、ユーザー同士のやり取り、アプリ内の購入、位置情報の共有は、すべて「いいえ」です。
（接続中のネットワーク名を読み取りますが、PC の外には送らず、位置情報も使いません）

## 6.5 ストア登録情報（Store listings）

著作権と商標の情報は `© 2026 SystemAzmax`、開発元は `SystemAzmax` です（日本語・英語とも）。

### 日本語

製品名: `テザリングキーパー`

説明:

```
テザリングキーパーは、iPhone のインターネット共有（テザリング）、Wi-Fi ルーター、モバイル回線、一部の VPN などで起きる「しばらく通信が無いと切れる」切断を起きにくくするアプリです。

一定の間隔で小さな通信を送り、接続を保ちます。通信方式は、通信量が最小の Ping (ICMP) のほか、HTTP HEAD、TCP 接続から選べます。タスクトレイに常駐し、Windows へのサインイン時に起動することもできます。

通信の失敗が続いたときは通知で知らせ、トレイのアイコンに赤い印を付けます。テザリングのネットワークにつないだときだけ動かす、決まった時刻や通信量の上限で止める、動作中はスリープしない、といった使い方もできます。

本アプリは個人情報を収集せず、広告もありません。切断を完全に防げることは保証できません。
```

製品の機能（1 行に 1 つ）:

```
一定の間隔で小さな通信を送り、アイドル切断を起きにくくする
Ping (ICMP)、HTTP HEAD、TCP 接続の 3 つの通信方式と、失敗したときの予備の通信方式
通信の失敗が続いたときと、復旧したときの通知
指定したネットワーク（テザリングなど）に接続したら開始し、切断したら停止
日時の予約、毎週の決まった時刻、通信量の上限での自動停止
動作中のスリープの抑止（オプション）
時間帯ごとの失敗回数の集計と CSV の書き出し
タスクトレイ常駐と、Windows へのサインイン時の自動起動
日本語と英語の画面
```

キーワード（7 つまで）: `テザリング`、`インターネット共有`、`キープアライブ`、`切断防止`、`Ping`、`Wi-Fi`、`モバイル回線`

### 英語

製品名: `TetherKeeper`

説明:

```
TetherKeeper helps prevent idle disconnections on iPhone Personal Hotspot (tethering), Wi-Fi routers, mobile connections, and some VPNs that drop the connection when there is no traffic for a while.

The app sends a small amount of traffic at regular intervals to keep the connection alive. Choose Ping (ICMP), which uses the least data, HTTP HEAD, or TCP Connect. It runs in the notification area and can start when you sign in to Windows.

When failures continue, the app notifies you and shows a red dot on the tray icon. You can also run it only while connected to your tethering network, stop it at a set time or when a data limit is reached, and keep the PC from sleeping while it runs.

The app does not collect personal information and has no ads. It cannot guarantee that the connection never drops.
```

製品の機能（1 行に 1 つ）:

```
Sends a small amount of traffic at regular intervals to prevent idle disconnections
Three methods (Ping, HTTP HEAD, and TCP Connect), plus a fallback method used when a run fails
Notifications when failures continue and when the connection recovers
Starts when you connect to a specified network (such as tethering) and stops when you disconnect
Automatic stop at a scheduled time, at a set time every week, or at a data limit
Optional prevention of automatic sleep while running
Failures counted by time of day, with export to CSV
Runs in the notification area and can start when you sign in to Windows
English and Japanese screens
```

キーワード（7 つまで）: `tethering`、`personal hotspot`、`keep alive`、`ping`、`idle disconnect`、`Wi-Fi`、`connection`

### スクリーンショット

- 1366×768 以上の PNG で、モニター画面（動作中）、履歴画面、設定画面、トレイのメニューの 4 枚を目安に撮ります。
- 英語の登録情報には、設定画面の「言語（Language）」を「English」にして再起動した画面で撮ります。
- 自宅のネットワーク名や、ログのファイルの場所（ユーザー名を含む）が写らないようにします。
  ネットワーク名は「iPhone」など一般的な名前にし、ログは「ログ消去」で消してから撮ります。

## 6.6 提出オプション（Submission options）

審査をする人は英語で読むため、どちらも英語で書きます。

認定の注意事項（Notes for certification）:

```
This app is a desktop app (.NET MAUI / WinUI 3 / .NET 10) packaged as MSIX.
No account or sign-in is required.
The main window opens at launch. Selecting the close button (X) moves the app to the notification area (system tray); select the tray icon to show the window again, and use "Exit" on the Monitor screen or in the tray menu to exit.
To test, select "Start" on the Monitor screen. The app sends a ping to 1.1.1.1 every 30 seconds, and the results appear in the log. The language can be switched between English and Japanese on the Settings screen.
Starting the app at sign-in (windows.startupTask) is disabled by default and can be turned on in the Settings screen.
```

制限付き機能（`runFullTrust`）:

```
This app is a desktop application (.NET MAUI / WinUI 3 / .NET 10), not a UWP app.
The runFullTrust capability is required to run the desktop executable inside the MSIX package.
The app does not request administrator privileges. It sends small network packets (ping, HTTP HEAD, or TCP connect) to the destination the user sets, reads the names of connected networks only when the user turns on the network-based start and stop, and stores its settings and logs in its own package folder.
```
