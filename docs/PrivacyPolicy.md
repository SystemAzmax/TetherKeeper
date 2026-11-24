# テザリングキーパー（TetherKeeper）プライバシーポリシー / Privacy Policy

[日本語](#日本語) | [English](#english)

## 日本語

制定日: 2026 年 11 月 21 日

SystemAzmax（以下「開発者」）は、Windows 用アプリ「テザリングキーパー」（英語名: TetherKeeper、以下「本アプリ」）における
利用者の情報の取り扱いについて、次のとおり定めます。

### 収集する情報

本アプリは、利用者の個人情報を収集しません。
本アプリには、利用者の情報を開発者や第三者に送信する機能はありません。
広告の表示や、利用状況の収集（アクセス解析など）も行いません。

### 本アプリが行う通信

本アプリは、ネットワークの切断を起きにくくするため、利用者が開始したときに、
利用者が設定した送信先へ、一定の間隔で小さな通信を行います。
送信先と通信の内容は次のとおりです。

| 通信方式 | 既定の送信先 | 通信の内容 |
| --- | --- | --- |
| Ping (ICMP) | `1.1.1.1`（Cloudflare, Inc. の公開 DNS サーバー） | ICMP エコー要求（Ping） |
| HTTP HEAD | `http://www.msftconnecttest.com/connecttest.txt`（Microsoft の接続確認用のページ） | HTTP の HEAD 要求（ページの見出しだけを要求し、本文は受け取りません） |
| TCP Connect | `1.1.1.1` のポート 443 | TCP の接続と、すぐの切断（データは送りません） |

- これらの通信には、利用者の個人情報や、PC に保存した情報は含まれません。
- インターネットの通信の仕組み上、送信先には、利用者の IP アドレスと通信した日時が伝わります。
  送信先での情報の取り扱いは、送信先の運営者の方針に従います。
- 送信先は、利用者が「設定」画面で変更できます。送信先にホスト名を指定した場合は、その名前を DNS で問い合わせます。
- 利用者が「予備の通信方式」を選んだ場合は、通信方式で失敗したときだけ、予備の方式の送信先にも同じように通信します。

「使い方」ボタンを押すと、使い方ガイド（GitHub のページ）を Web ブラウザーで開きます。

### PC に保存する情報

本アプリは、動作に必要な次の情報を、利用者の PC の中に保存します。
これらが開発者に送信されることはありません。

- 設定（通信方式、送信先、実行間隔、通信量の上限、自動停止の日時、自動で開始・停止するネットワークの名前など）
- 通信量の累計
- 動作の記録（ログ）。通信の結果、送信先、通信量の計測に使ったネットワーク アダプターの名前、自動で開始・停止したネットワークの名前、エラーの内容などが記録されます。
  エラーの内容には、ファイルの場所（Windows のユーザー名を含むことがあります）が含まれることがあります。
  30 日を過ぎたログは自動で削除されます
- 通信の結果の履歴（日時、成否、通信方式、送信先、応答時間、エラーの内容）。時間帯ごとの集計に使います。30 日を過ぎた履歴は自動で削除されます。
  利用者が「CSV に書き出す」を選んだときだけ、PC のダウンロード フォルダーに書き出します

通信量は、PC のネットワーク アダプターの送受信量を、PC の中で数えています。通信の内容は読み取りません。

「指定したネットワークに接続したら開始し、切断したら停止する」を使う場合、本アプリは、PC が接続しているネットワークの名前（Wi-Fi のネットワーク名（SSID）など）を Windows から読み取ります。
読み取った名前は、設定画面の一覧の表示と、指定したネットワークにつながっているかの判定にだけ使います。保存するのは、利用者が選んだネットワークの名前だけです。位置情報は使いません。

Microsoft Store からインストールした場合、これらの情報は、本アプリをアンインストールすると削除されます。

### Microsoft の製品が扱う情報

Windows や Microsoft Store など、Microsoft の製品やサービスが扱う情報は、
[Microsoft のプライバシーに関する声明](https://privacy.microsoft.com/privacystatement) に従って取り扱われます。

### このプライバシーポリシーの変更

このプライバシーポリシーを変更する場合は、このページで公開します。

### お問い合わせ

本アプリやこのプライバシーポリシーについてのお問い合わせは、
[GitHub Issues](https://github.com/SystemAzmax/TetherKeeper/issues) でお受けします。

---

## English

Effective date: November 21, 2026

This privacy policy describes how SystemAzmax (the "developer") handles user information
in TetherKeeper (Japanese name: テザリングキーパー; the "app"), an app for Windows.

### Information We Collect

The app does not collect personal information.
The app has no feature that sends user information to the developer or any third party.
The app does not show ads or collect usage data (such as analytics).

### Network Traffic Sent by the App

To make network connections less likely to drop, the app sends a small amount of traffic
at regular intervals to the destination you set, after you start it.
The destinations and the content of the traffic are as follows.

| Method | Default destination | Content |
| --- | --- | --- |
| Ping (ICMP) | `1.1.1.1` (a public DNS server of Cloudflare, Inc.) | ICMP echo requests (pings) |
| HTTP HEAD | `http://www.msftconnecttest.com/connecttest.txt` (a Microsoft page for connectivity checks) | HTTP HEAD requests (which request only the headers of the page, not the body) |
| TCP Connect | Port 443 of `1.1.1.1` | A TCP connection that is closed immediately (no data is sent) |

- This traffic does not include your personal information or any information stored on your PC.
- Because of how internet communication works, the destination receives your IP address and the time of the communication.
  The destination handles this information in accordance with its operator's policies.
- You can change the destinations on the "Settings" screen. If you specify a host name as the destination, the app looks up that name using DNS.
- If you choose a "Fallback method", the app also sends the same kind of traffic to the destination of the fallback method, but only when the main method fails.

When you select the "User guide" button, the app opens the user guide (a GitHub page) in your web browser.

### Information Stored on Your PC

The app stores the following information on your PC so that it can work.
This information is never sent to the developer.

- Settings, such as the method, destinations, interval, data limit, the scheduled stop time, and the name of the network used to start and stop automatically
- The total data usage
- Activity logs. They include the results of each run, the destination, the name of the network adapter used to measure data usage, the name of the network used to start or stop automatically, and error details.
  Error details may include file locations, which may contain your Windows user name.
  Logs older than 30 days are deleted automatically
- The history of results (the time, whether it succeeded, the method, the destination, the response time, and error details). It is used to count failures by time of day. History older than 30 days is deleted automatically.
  The app writes it to the Downloads folder on your PC only when you select "Export to CSV"

The app counts data usage on your PC from the amount of data sent and received by the network adapter. It does not read the content of any traffic.

If you use "Start when connected to the specified network and stop when disconnected", the app reads the names of the networks your PC is connected to (such as Wi-Fi network names (SSIDs)) from Windows.
The app uses these names only to show the list on the Settings screen and to check whether your PC is connected to the specified network. It saves only the network name you choose. The app does not use location information.

If you installed the app from the Microsoft Store, this information is deleted when you uninstall the app.

### Information Handled by Microsoft Products

Information handled by Microsoft products and services, such as Windows and the Microsoft Store,
is handled in accordance with the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

### Changes to This Privacy Policy

If we change this privacy policy, we will publish the changes on this page.

### Contact

For questions about the app or this privacy policy, please use
[GitHub Issues](https://github.com/SystemAzmax/TetherKeeper/issues).
