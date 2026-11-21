# TetherKeeper User Guide

English | [日本語](UserGuide_ja.md)

TetherKeeper sends a small amount of traffic at regular intervals so that iPhone Personal Hotspot (tethering), Wi-Fi routers, mobile connections, and similar connections are less likely to drop when idle.
It cannot guarantee that the connection never drops.

The app's screens are in Japanese only. In this guide, each item is shown with the Japanese text you see on the screen, followed by its meaning in English.

- [1. Getting Started](#1-getting-started)
- [2. Screens and Features](#2-screens-and-features)
- [3. Troubleshooting](#3-troubleshooting)
- [4. Uninstalling](#4-uninstalling)
- [Contact](#contact)

## 1. Getting Started

### Start the App

Start "TetherKeeper" from the Start menu.
The "モニター" (Monitor) screen opens, and the status is "停止中" (Stopped).
The app sends nothing at first. Select "開始" (Start) to begin the keep-alive (regular traffic).

### Find the Tray Icon

When you select "×" on the window, the app does not exit. It moves to the notification area (system tray) and keeps running.
The icon appears at the right end of the taskbar, near the clock.
If you can't see it, select "^" (Show hidden icons) on the taskbar. The icon is there.

To keep it visible, drag the icon from "^" to the taskbar near the clock.

- Select the icon to show the window.
- Right-click the icon to choose "ウィンドウを表示" (Show window), "キープアライブを開始" (Start keep-alive; "キープアライブを停止" (Stop keep-alive) while running), or "終了" (Exit).
- To exit the app, select "終了" (Exit) on the Monitor screen or in the tray menu.

### Start the Keep-Alive

1. On the "モニター" (Monitor) screen, select "開始" (Start).
2. The status changes to "動作中" (Running), and the app sends traffic every 30 seconds (default). The results appear in the log at the bottom of the screen.
3. If "失敗" (Failed) continues, switch the method on the "設定" (Settings) screen (see [3. Troubleshooting](#3-troubleshooting)).

To run it automatically when you start your PC, turn on both "Windows ログイン時にアプリを起動する" (Start the app when you sign in to Windows) and "アプリ起動時に自動で開始する" (Start automatically when the app starts) on the "設定" (Settings) screen, and then select "保存" (Save).

## 2. Screens and Features

Use "モニター" (Monitor) and "設定" (Settings) at the bottom of the window to switch screens.

### モニター (Monitor)

| Item | Description |
| --- | --- |
| Status | Shows "停止中" (Stopped) or "動作中" (Running), the method, destination, and interval, and the seconds until the next run. If failures continue, it shows "動作中（N 回連続失敗）" (Running, N consecutive failures) |
| 開始 / 停止 (Start / Stop) | Starts or stops the keep-alive |
| 今すぐ実行 (Run now) | Sends traffic once right away, without waiting for the interval |
| 終了 (Exit) | Exits the app ("×" only moves the app to the tray; it does not exit) |
| 自動停止 (Auto stop) | Choose a date and time and select "予約" (Schedule) to stop the keep-alive at that time. The app does not exit. The schedule is cleared when it stops. Select "解除" (Cancel) to cancel the schedule |
| 通信量 (Data usage) | The total data counted while the keep-alive is running. If you set a limit on the Settings screen, the keep-alive stops when it reaches the limit. Select "リセット" (Reset) to set it back to 0 |
| 合計 / 成功 / 失敗 / 連続失敗 (Total / Succeeded / Failed / Consecutive failures) | Number of runs. Select "統計リセット" (Reset statistics) to set them back to 0 |
| ログ (Log) | Records such as the results of each run. "ログ消去" (Clear log) clears only the log on the screen (the log files remain). "保存先" (Location) opens the folder where the logs and settings are saved |

About data usage:

- The app counts the data sent and received by the network adapter used for internet traffic (if you are connected to both wired LAN and Wi-Fi, the one Windows chooses). This includes traffic from other apps.
- Because it measures every 5 seconds, it may stop slightly after the limit is exceeded.
- Stopping the keep-alive does not stop traffic from other apps.
- While the limit is reached, you can't select "開始" (Start). Select "リセット" (Reset) to be able to start again.

### 設定 (Settings)

After making changes, select "保存" (Save). "既定値に戻す" (Restore defaults) restores the default values in the input fields (they are not applied until you select "保存" (Save)).

| Item | Description |
| --- | --- |
| 通信方式 (Method) | Choose "Ping (ICMP)", "HTTP HEAD", or "TCP Connect". The default is "Ping (ICMP)" |
| Ping 送信先（ホスト名 / IP アドレス） (Ping destination: host name or IP address) | Where to send pings. The default is `1.1.1.1` |
| HTTP HEAD 送信先 URL (HTTP HEAD destination URL) | Where to send HTTP HEAD requests. The default is `http://www.msftconnecttest.com/connecttest.txt` |
| TCP 接続先ホスト / ポート (TCP host / port) | Where to connect with TCP Connect. The defaults are `1.1.1.1` and `443` |
| 実行間隔（秒） (Interval in seconds) | How often to send traffic, from 5 to 3600 seconds. The default is 30 seconds. If your connection still drops, try 15 to 30 seconds |
| タイムアウト（ミリ秒） (Timeout in milliseconds) | How long to wait for a response, from 500 to 30000 milliseconds. It must be shorter than the interval. The default is 3000 milliseconds |
| 通信量の上限（MB） (Data limit in MB) | Stops when the total data usage reaches this value. 0 means no limit (default) |
| アプリ起動時に自動で開始する (Start automatically when the app starts) | When on, the keep-alive starts when the app starts |
| Windows ログイン時にアプリを起動する (Start the app when you sign in to Windows) | When on, the app starts in the tray when you sign in to Windows |

Differences between the methods:

| Method | Best for |
| --- | --- |
| Ping (ICMP) | Uses the least data (about 0.35 MB per day at 30-second intervals). Fails on networks that block ICMP |
| HTTP HEAD | More likely to work behind a proxy or on networks that block ICMP. Uses more data than Ping |
| TCP Connect | Connects and immediately disconnects. Connections to the port (such as 443) must be allowed |

### App Information

These buttons are at the bottom of the "設定" (Settings) screen.

| Item | Description |
| --- | --- |
| このアプリについて (About this app) | Shows the app version and other information |
| ライセンス情報 (License information) | Shows the licenses of the software this app uses |
| 使い方 (User guide) | Opens this page (the user guide) in your web browser |

## 3. Troubleshooting

### The window doesn't appear after starting the app

The app may be in the tray. See [Find the Tray Icon](#find-the-tray-icon) and select the icon.
When the app starts at Windows sign-in, it starts in the tray without showing the window.

### "失敗" (Failed) keeps appearing

- If "Ping (ICMP)" fails, the network may be blocking ICMP. Try switching to "HTTP HEAD" on the Settings screen.
- On networks that use a proxy, such as at work, use "HTTP HEAD".
- If you changed the destination, check that it is correct. "既定値に戻す" (Restore defaults) brings back the default destinations.

### The connection still drops

Try a shorter "実行間隔（秒）" (Interval in seconds), such as 15 to 30 seconds, on the Settings screen.
Depending on the connection or device, it may drop even while traffic is being sent.

### I can't start the keep-alive

If "上限に達したため、開始できません。" (The limit has been reached, so it can't start.) appears, the total data usage has reached "通信量の上限（MB）" (Data limit in MB).
Select "リセット" (Reset) under data usage on the Monitor screen, or change the limit on the Settings screen.

### I can't turn on "Windows ログイン時にアプリを起動する" (Start the app when you sign in to Windows)

If TetherKeeper is disabled in "Startup apps" in Task Manager, the app can't turn it on (this is how Windows works).
Open Task Manager and set TetherKeeper to "Enabled" in "Startup apps".

### After the PC wakes from sleep

When the PC wakes from sleep, the app resumes sending traffic after 5 seconds. Nothing is sent while the PC is asleep.

## 4. Uninstalling

In the Start menu, right-click "TetherKeeper" and choose "Uninstall".

- Your settings, total data usage, and logs are deleted when you uninstall the app.
- The "Start the app when you sign in to Windows" setting is also deleted.

## Contact

To report a bug or make a request, please use [GitHub Issues](https://github.com/SystemAzmax/TetherKeeper/issues).
For privacy, see the [Privacy Policy](PrivacyPolicy.md).
