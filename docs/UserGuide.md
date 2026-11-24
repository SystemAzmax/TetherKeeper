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
- Right-click the icon to see the current status at the top of the menu (whether it is running, the last result, the seconds until the next run, and the scheduled auto stop).
  Below that, you can choose "ウィンドウを表示" (Show window), "キープアライブを開始" (Start keep-alive; "キープアライブを停止" (Stop keep-alive) while running), "今すぐ実行" (Run now), "自動停止を予約" (Schedule auto stop: stop in 1 hour, stop in 2 hours, or cancel the schedule), or "終了" (Exit).
  Choosing a time in "自動停止を予約" (Schedule auto stop) replaces any existing schedule.
- To exit the app, select "終了" (Exit) on the Monitor screen or in the tray menu.

### Notice When the Connection Fails

If the traffic fails 3 times in a row (default), the app assumes that the tethering or other connection has dropped and shows a Windows notification.
While the failures continue, a red dot appears at the lower right of the tray icon.
When the traffic succeeds again, the app shows "通信が復旧しました" (The connection has recovered), and the red dot disappears.

- Select the notification to show the window.
- You can change the number of failures and turn the notifications on or off on the "設定" (Settings) screen.

### Start the Keep-Alive

1. On the "モニター" (Monitor) screen, select "開始" (Start).
2. The status changes to "動作中" (Running), and the app sends traffic every 30 seconds (default). The results appear in the log at the bottom of the screen.
3. If "失敗" (Failed) continues, switch the method on the "設定" (Settings) screen (see [3. Troubleshooting](#3-troubleshooting)).

To run it automatically when you start your PC, turn on both "Windows ログイン時にアプリを起動する" (Start the app when you sign in to Windows) and "アプリ起動時に自動で開始する" (Start automatically when the app starts) on the "設定" (Settings) screen, and then select "保存" (Save).

### Run Only While Tethering

You can have the app start when you connect to tethering and stop when the connection ends, so it doesn't run on your home Wi-Fi or other networks.

1. Connect your PC to tethering.
2. On the "設定" (Settings) screen, turn on "指定したネットワークに接続したら開始し、切断したら停止する" (Start when connected to the specified network and stop when disconnected).
3. In "接続中のネットワークから選ぶ" (Choose from connected networks), choose the tethering network. For Wi-Fi, it appears with your iPhone's name (the network name).
4. Select "保存" (Save).

- The app starts or stops about 5 seconds after the connection changes.
- When it stops because the connection ended, the app shows a notification (if "失敗が続いたときと、復旧したときに通知する" (Notify when failures continue and when the connection recovers) is on).
- If you select "停止" (Stop) yourself while connected to tethering, the app doesn't start automatically until you reconnect.
- If you select "開始" (Start) yourself on another network, the app doesn't stop automatically.
- The app must be running to start the keep-alive. It's convenient to also turn on "Windows ログイン時にアプリを起動する" (Start the app when you sign in to Windows).

## 2. Screens and Features

Use "モニター" (Monitor), "履歴" (History), and "設定" (Settings) at the bottom of the window to switch screens.

### モニター (Monitor)

| Item | Description |
| --- | --- |
| Status | Shows "停止中" (Stopped) or "動作中" (Running), the method, destination, and interval, and the seconds until the next run. If failures continue, it shows "動作中（N 回連続失敗）" (Running, N consecutive failures) |
| 開始 / 停止 (Start / Stop) | Starts or stops the keep-alive |
| 今すぐ実行 (Run now) | Sends traffic once right away, without waiting for the interval |
| 終了 (Exit) | Exits the app ("×" only moves the app to the tray; it does not exit) |
| 自動停止 (Auto stop) | Choose a date and time and select "予約" (Schedule) to stop the keep-alive at that time. Select "1 時間後" (In 1 hour) or "2 時間後" (In 2 hours) to schedule a stop that long from now. The schedule is cleared when it stops. Select "解除" (Cancel) to cancel the schedule. The app does not exit (it exits if you turn on "自動停止したらアプリも終了する" (Exit the app when it stops automatically) on the Settings screen). If you use the weekly auto stop, its schedule is also shown |
| 通信量 (Data usage) | The total data counted while the keep-alive is running. If you set a limit on the Settings screen, the keep-alive stops when it reaches the limit. Select "リセット" (Reset) to set it back to 0 |
| 合計 / 成功 / 失敗 / 連続失敗 (Total / Succeeded / Failed / Consecutive failures) | Number of runs. A run that succeeds with the fallback method counts as succeeded, and that number is shown below. Select "統計リセット" (Reset statistics) to set them back to 0 |
| ログ (Log) | Records such as the results of each run. "ログ消去" (Clear log) clears only the log on the screen (the log files remain). "保存先" (Location) opens the folder where the logs and settings are saved |

About data usage:

- The app counts the data sent and received by the network adapter used for internet traffic (if you are connected to both wired LAN and Wi-Fi, the one Windows chooses). This includes traffic from other apps.
- Because it measures every 5 seconds, it may stop slightly after the limit is exceeded.
- Stopping the keep-alive does not stop traffic from other apps.
- While the limit is reached, you can't select "開始" (Start). Select "リセット" (Reset) to be able to start again.

### 履歴 (History)

This screen counts the results recorded while the keep-alive was running, by time of day. Use it to see when the connection tends to drop.

| Item | Description |
| --- | --- |
| Period | Choose "直近 24 時間" (Last 24 hours), "直近 7 日" (Last 7 days), or "直近 30 日" (Last 30 days) |
| 更新 (Refresh) | Counts again with the latest records |
| CSV に書き出す (Export to CSV) | Writes the results for the selected period to a CSV file in your Downloads folder and shows it in File Explorer. You can open it in Excel |
| Total | The number of runs in the period, the numbers of successes and failures, and the failure rate |
| 時間帯ごとの失敗 (Failures by time of day) | For each hour from 0 to 23, shows the number of runs, the number of failures, and the failure rate. The red bar shows the failure rate; the hour with the highest rate has the longest bar |

- Runs that succeed with the fallback method count as successes.
- Records are kept for 30 days. They are saved as daily CSV files in `history`, in the same folder as the logs (open it with "保存先" (Location) on the Monitor screen).

### 設定 (Settings)

After making changes, select "保存" (Save). "既定値に戻す" (Restore defaults) restores the default values in the input fields (they are not applied until you select "保存" (Save)).

| Item | Description |
| --- | --- |
| 通信方式 (Method) | Choose "Ping (ICMP)", "HTTP HEAD", or "TCP Connect". The default is "Ping (ICMP)" |
| 予備の通信方式 (Fallback method) | Only when the method fails, the app tries once more with this method. A run counts as failed only when both fail. The destination is taken from that method's field. The default is "使わない" (Don't use) (see ["失敗" (Failed) keeps appearing](#失敗-failed-keeps-appearing)) |
| Ping 送信先（ホスト名 / IP アドレス） (Ping destination: host name or IP address) | Where to send pings. The default is `1.1.1.1` |
| HTTP HEAD 送信先 URL (HTTP HEAD destination URL) | Where to send HTTP HEAD requests. The default is `http://www.msftconnecttest.com/connecttest.txt` |
| TCP 接続先ホスト / ポート (TCP host / port) | Where to connect with TCP Connect. The defaults are `1.1.1.1` and `443` |
| 実行間隔（秒） (Interval in seconds) | How often to send traffic, from 5 to 3600 seconds. The default is 30 seconds. If your connection still drops, try 15 to 30 seconds. Below it, the app shows an estimate of the data used with the selected method and interval (per hour and per 24 hours) |
| タイムアウト（ミリ秒） (Timeout in milliseconds) | How long to wait for a response, from 500 to 30000 milliseconds. It must be shorter than the interval. The default is 3000 milliseconds |
| 通信量の上限（MB） (Data limit in MB) | Stops when the total data usage reaches this value. 0 means no limit (default) |
| 毎週決まった時刻に停止する (Stop at a set time every week) | When on, the keep-alive stops at the set time on the days you choose (only while it is running). Unlike a scheduled date and time, this setting is not cleared when it stops. The default is off |
| 自動停止したらアプリも終了する (Exit the app when it stops automatically) | When on, the app also exits when the keep-alive stops at a scheduled date and time or at the weekly time. It doesn't exit when it stops because of the data limit or a network disconnection. The default is off |
| 通知する連続失敗の回数 (Consecutive failures before notifying) | When the traffic fails this many times in a row, the app shows a notification and adds a red dot to the tray icon. From 1 to 100. The default is 3 |
| 失敗が続いたときと、復旧したときに通知する (Notify when failures continue and when the connection recovers) | When off, the app doesn't show notifications (the red dot still appears on the tray icon). The default is on |
| キープアライブの動作中はスリープしない (Don't sleep while the keep-alive is running) | When on, the PC doesn't go to sleep automatically while the keep-alive is running, even if you leave it idle. The default is off (see [After the PC wakes from sleep](#after-the-pc-wakes-from-sleep)) |
| 指定したネットワークに接続したら開始し、切断したら停止する (Start when connected to the specified network and stop when disconnected) | When on, the keep-alive starts when you connect to the network in "ネットワーク名" (Network name) and stops when you disconnect. The default is off (see [Run Only While Tethering](#run-only-while-tethering)) |
| ネットワーク名 (Network name) | The name of the network that starts and stops the keep-alive. You can choose it in "接続中のネットワークから選ぶ" (Choose from connected networks). Select "更新" (Refresh) to update the list |
| アプリ起動時に自動で開始する (Start automatically when the app starts) | When on, the keep-alive starts when the app starts |
| Windows ログイン時にアプリを起動する (Start the app when you sign in to Windows) | When on, the app starts in the tray when you sign in to Windows |

About the data estimate:

- It estimates only the keep-alive's own traffic. The actual amount varies with the connection and with IPv4 or IPv6.
- It doesn't include traffic from other apps, name resolution (when the destination is a host name), or retries with the fallback method.
- The amount counted for "通信量の上限（MB）" (Data limit in MB) includes traffic from other apps, so it is larger than the estimate.

Differences between the methods:

| Method | Best for |
| --- | --- |
| Ping (ICMP) | Uses the least data (at 30-second intervals, about 14 to 19 KB per hour, or about 0.35 to 0.46 MB per 24 hours). Fails on networks that block ICMP |
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
- Sometimes only one method fails, because of a problem at the destination or because the network blocks it. If you choose a "予備の通信方式" (Fallback method), the app tries again with another method when a run fails, and the run counts as succeeded if the connection is working (the log shows "予備で成功" (Succeeded with fallback)). If the fallback keeps succeeding, switching the method to the fallback method reduces the extra traffic from retrying.

### Failures continue, but no notification appears

- On the Settings screen, check that "失敗が続いたときと、復旧したときに通知する" (Notify when failures continue and when the connection recovers) is on.
- In Windows Settings > System > Notifications, check that notifications are on, both overall and for TetherKeeper.
- While "Do not disturb" (Focus) is on, notifications don't pop up. They only go to the notification center.

### The connection still drops

Try a shorter "実行間隔（秒）" (Interval in seconds), such as 15 to 30 seconds, on the Settings screen.
Depending on the connection or device, it may drop even while traffic is being sent.

### I can't start the keep-alive

If "上限に達したため、開始できません。" (The limit has been reached, so it can't start.) appears, the total data usage has reached "通信量の上限（MB）" (Data limit in MB).
Select "リセット" (Reset) under data usage on the Monitor screen, or change the limit on the Settings screen.

### The keep-alive doesn't start when I connect to tethering

- On the Settings screen, check that "ネットワーク名" (Network name) matches the name of the tethering network (uppercase and lowercase letters are distinguished). The surest way is to select "更新" (Refresh) while connected to tethering and choose the network again from the list.
- When you connect with USB or Bluetooth, the network has a different name from Wi-Fi. If you change how you connect, choose the network again.
- While the data limit is reached, the keep-alive doesn't start (see [I can't start the keep-alive](#i-cant-start-the-keep-alive)).
- If you selected "停止" (Stop) yourself while connected to tethering, the app doesn't start automatically until you reconnect.

### I can't turn on "Windows ログイン時にアプリを起動する" (Start the app when you sign in to Windows)

If TetherKeeper is disabled in "Startup apps" in Task Manager, the app can't turn it on (this is how Windows works).
Open Task Manager and set TetherKeeper to "Enabled" in "Startup apps".

### After the PC wakes from sleep

When the PC wakes from sleep, the app resumes sending traffic after 5 seconds. Nothing is sent while the PC is asleep.

If tethering drops while the PC is asleep, turn on "キープアライブの動作中はスリープしない" (Don't sleep while the keep-alive is running) on the Settings screen.
While the keep-alive is running, the PC doesn't go to sleep automatically, even if you leave it idle. Only sleep is prevented; the screen still turns off as usual.

- The PC still goes to sleep when you close the lid, press the power button, or select "Sleep" from the Start menu (this is how Windows works).
- On laptops, battery use increases.
- On laptops that support Modern Standby and are running on battery, the PC goes to sleep 5 minutes after the Windows sleep timeout passes (this is how Windows works).
- When you stop the keep-alive, the PC goes back to its usual sleep settings.

## 4. Uninstalling

In the Start menu, right-click "TetherKeeper" and choose "Uninstall".

- Your settings, total data usage, logs, and history of results are deleted when you uninstall the app (CSV files you exported to the Downloads folder remain).
- The "Start the app when you sign in to Windows" setting is also deleted.

## Contact

To report a bug or make a request, please use [GitHub Issues](https://github.com/SystemAzmax/TetherKeeper/issues).
For privacy, see the [Privacy Policy](PrivacyPolicy.md).
