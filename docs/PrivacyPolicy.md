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

「使い方」ボタンを押すと、使い方ガイド（GitHub のページ）を Web ブラウザーで開きます。

### PC に保存する情報

本アプリは、動作に必要な次の情報を、利用者の PC の中に保存します。
これらが開発者に送信されることはありません。

- 設定（通信方式、送信先、実行間隔、通信量の上限、自動停止の日時など）
- 通信量の累計
- 動作の記録（ログ）。通信の結果、送信先、通信量の計測に使ったネットワーク アダプターの名前、エラーの内容などが記録されます。
  エラーの内容には、ファイルの場所（Windows のユーザー名を含むことがあります）が含まれることがあります。
  30 日を過ぎたログは自動で削除されます

通信量は、PC のネットワーク アダプターの送受信量を、PC の中で数えています。通信の内容は読み取りません。

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
- You can change the destinations on the "設定" (Settings) screen. If you specify a host name as the destination, the app looks up that name using DNS.

When you select the "使い方" (User guide) button, the app opens the user guide (a GitHub page) in your web browser.

### Information Stored on Your PC

The app stores the following information on your PC so that it can work.
This information is never sent to the developer.

- Settings, such as the method, destinations, interval, data limit, and the scheduled stop time
- The total data usage
- Activity logs. They include the results of each run, the destination, the name of the network adapter used to measure data usage, and error details.
  Error details may include file locations, which may contain your Windows user name.
  Logs older than 30 days are deleted automatically

The app counts data usage on your PC from the amount of data sent and received by the network adapter. It does not read the content of any traffic.

If you installed the app from the Microsoft Store, this information is deleted when you uninstall the app.

### Information Handled by Microsoft Products

Information handled by Microsoft products and services, such as Windows and the Microsoft Store,
is handled in accordance with the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

### Changes to This Privacy Policy

If we change this privacy policy, we will publish the changes on this page.

### Contact

For questions about the app or this privacy policy, please use
[GitHub Issues](https://github.com/SystemAzmax/TetherKeeper/issues).
