# TetherKeeper

English | [日本語](README_ja.md)

iPhone Personal Hotspot (tethering), Wi-Fi routers, mobile connections, and some VPNs may drop the connection when there is no traffic for a while.
TetherKeeper sends a small amount of traffic at regular intervals so that these idle disconnections are less likely to happen. Its Japanese name is テザリングキーパー, which Windows shows when the display language is Japanese.

The app's screens are in English or Japanese. By default they follow the Windows display language, and you can change the language on the Settings screen.

> There is no guarantee that tethering never drops.

## Features

- Three methods (switch on the Settings screen)
  - **Ping (ICMP)**: Uses the least data and doesn't require administrator rights (default)
  - **HTTP HEAD**: For networks that block ICMP or use a proxy
  - **TCP Connect**: Disconnects as soon as the connection is established
- Optional fallback method: only when the method fails, the app tries once more with another method, and records a failure only when both fail
- Configurable interval (5 to 3600 seconds) and timeout. The Settings screen shows an estimate of the data used per hour (and per 24 hours)
- Shows the number of successes and failures, consecutive failures, and the log
- "Run now" button
- When the PC wakes from sleep, runs again after 5 seconds
- Optionally prevents automatic sleep while the keep-alive is running (off by default. The screen still turns off)
- Optionally starts when you connect to a specified network (such as your iPhone's tethering) and stops when you disconnect (off by default)
- Automatic stop at a specified date and time (choose the date and time on the "Monitor" screen and select "Schedule", or select "In 1 hour" / "In 2 hours". The schedule is cleared automatically after it stops)
- Automatic stop at a set time on the days of the week you choose, and an option to exit the app when it stops automatically
- Automatic stop at a data limit (set the limit in MB on the Settings screen. The app adds up the data used while the keep-alive is running until you select "Reset" on the Monitor screen, and stops when it reaches the limit. You can't start while the limit is reached)
  - The app counts the data sent and received by the network adapter used for internet traffic (if you are connected to both wired LAN and Wi-Fi, the one Windows chooses). This includes traffic from other apps. The log shows which adapter is counted
  - Because it measures every 5 seconds, it stops after exceeding the limit by the amount of traffic in that time. Stopping the keep-alive does not stop traffic from other apps
- Notifications when failures continue and when the connection recovers (after 3 consecutive failures by default; the number can be changed on the Settings screen). While failures continue, the tray icon shows a red dot
- Start automatically when the app starts, and start the app when you sign in to Windows (starts in the tray)
- Runs in the notification area (system tray) ("×" moves the app to the tray. To exit, use the "Exit" button on the screen or the tray menu). The tray menu shows the current status and lets you run now and schedule an auto stop
- Log files (one file per day, kept for 30 days)
- "History" screen: counts failures by time of day for the last 24 hours, 7 days, or 30 days, and exports the results to CSV (the results are recorded separately from the logs, one CSV file per day, kept for 30 days)
- "App information" on the "Settings" screen shows About this app (version), License information, and the User guide

For details on each setting and troubleshooting, see the [User Guide](docs/UserGuide.md).
For how the app handles personal information and the network traffic it sends, see the [Privacy Policy](docs/PrivacyPolicy.md).
To report a bug or make a request, please use [GitHub Issues](https://github.com/SystemAzmax/TetherKeeper/issues).

## Requirements

- Windows 10 version 1809 or later / Windows 11
- For development: .NET 10 SDK and the MAUI workload (`maui-windows`)

## Build and Run

```powershell
# Start the Windows app
dotnet build TetherKeeper/TetherKeeper.csproj -f net10.0-windows10.0.19041.0
dotnet run --project TetherKeeper/TetherKeeper.csproj -f net10.0-windows10.0.19041.0

# Unit tests
dotnet test KeepAlive.Tests
```

In Visual Studio, open `TetherKeeper.slnx` and choose "Windows Machine" as the debug target.

> A macOS version (a menu bar app) is in development. How to build it on a Mac and submit it to the Mac App Store is described in [docs/MacBuildGuide.md](docs/MacBuildGuide.md) (in Japanese).

Settings are saved to `%LOCALAPPDATA%\TetherKeeper\settings.json`, and the total data usage is saved to `data-usage.json` in the same folder.
Logs are written to `logs\TetherKeeper_yyyyMMdd.log` in the same folder, one file per day, and files older than 30 days are deleted automatically.
The history of results is written to `history\TetherKeeper_yyyyMMdd.csv` in the same way.
The "Location" button on the Monitor screen opens the `logs` folder.
(These are the locations for the unpackaged app during development. The MSIX app uses a folder dedicated to the package, as described below.)

## Installing with MSIX

### Create the package (development PC)

```powershell
powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1
```

The following files are written to `artifacts\msix\`.

| File | Purpose |
|---|---|
| `TetherKeeper_<version>_Test\TetherKeeper_<version>_x64.msix` | The installer (it includes .NET and the Windows App SDK, so it can be installed on its own) |
| `TetherKeeper.cer` | The public key of the signing certificate. Used to trust the certificate on the PC where you install the app |

A self-signed certificate for signing (`CN=TetherKeeper`, valid for 5 years) is created automatically in the current user's certificate store the first time you run the script.

Because the signature is timestamped, the script needs an internet connection (only the hash of the signature is sent to the timestamp server). You can change the server with `-TimestampUrl`.

### On the PC where you install the app

1. **Trust the certificate (only the first time on each PC. Requires administrator rights)**

   Run the following in PowerShell as an administrator.

   ```powershell
   Import-Certificate -FilePath TetherKeeper.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

   To do this with the mouse, double-click `TetherKeeper.cer` → "Install Certificate" → Store Location "Local Machine" → "Place all certificates in the following store", and choose "Trusted People".

2. **Install (no administrator rights required. Run it with the account of the person who uses the app)**

   Double-click the `.msix` and select "Install". TetherKeeper (テザリングキーパー when the display language is Japanese) is added to the Start menu.

### Update

Raise `ApplicationDisplayVersion` in `TetherKeeper\TetherKeeper.csproj` (for example, `1.0.0` → `1.1.0`), run `Publish-Msix.ps1`, and double-click the new `.msix`. Settings and logs are kept (you can't update without changing the version).
Keep `ApplicationVersion` at `0` so that the package can be submitted to the Store (see "バージョンの上げ方" (How to raise the version) in [docs/BuildGuide.md](docs/BuildGuide.md)).

### Uninstall

Uninstall TetherKeeper from Settings → Apps → Installed apps. The settings and logs are deleted together with the package's folder.

### Differences from the unpackaged app

| Item | Unpackaged (running the exe directly during development) | MSIX |
|---|---|---|
| Where settings and logs are saved | `%LOCALAPPDATA%\TetherKeeper` | `%LOCALAPPDATA%\Packages\TetherKeeper_<publisher ID>\LocalState` (the "Location" button opens its `logs` folder) |
| Start at sign-in | The registry Run key | StartupTask (can also be switched in "Startup apps" in Task Manager) |
| Data when uninstalled | Kept | Deleted automatically |

If you disable start at sign-in in Task Manager, Windows doesn't allow the app's Settings screen to turn it back on. Turn it back on in "Startup apps" in Task Manager.

### Managing the signing certificate

The certificate used for signing (with its private key) exists only in the "Current User" certificate store on the PC where you created the package. If you lose it, for example when you replace the PC, a new certificate is created automatically. In that case, trust the new `TetherKeeper.cer` on each PC in the same way as step 1.

The private key is created as non-exportable, so you can't take it out of the PC or back it up. This prevents anyone who steals the private key from installing any package without warning on every PC that trusts this certificate.

Certificates created by earlier versions of the script have an exportable private key, so the script shows a warning. Run the following to switch to a new certificate. In this case too, you need to trust the new `TetherKeeper.cer` on each PC. Delete the old certificate in `certmgr.msc` (Current User → "Personal" → "Certificates").

```powershell
powershell -ExecutionPolicy Bypass -File build\Publish-Msix.ps1 -NewCertificate
```

## Solution Structure

```text
├─ KeepAlive.Core            Platform-independent logic
│   ├─ Models                AppSettings / KeepAliveResult / LogEntry / KeepAliveStatistics / DataUsage
│   ├─ Interfaces            IKeepAliveStrategy / ISettingsService / ILogService / IKeepAliveWorker / IDataUsageTracker, etc.
│   ├─ Services              KeepAliveWorker / SettingsService / LogService / KeepAliveStrategyResolver / DataUsageTracker
│   │                        FailureAlertTracker (decides when to notify about failures and recovery)
│   │                        NetworkAutoControl (starts and stops depending on the connected network)
│   │                        AutoStopScheduler (schedules and cancels the auto stop)
│   │                        ResultHistory / ResultCsv (records, counts, and exports the history of results)
│   │                        AppDisplayName (switches between the Japanese and English names) / AppAboutInfo (text for "About this app")
│   │                        AppLanguageStartup (applies the language chosen in the settings at startup)
│   ├─ Strategies            KeepAliveStrategy for Ping / HttpHead / TcpConnect
│   ├─ Configuration         DI registration (AddKeepAliveCore) and KeepAliveOptions
│   └─ Resources             CoreStrings.resx (English) / CoreStrings.ja.resx (Japanese): text for logs, errors, and notifications
├─ KeepAlive.Infrastructure  Platform-dependent implementations
│   ├─ Settings              JsonFileSettingsStore / JsonFileDataUsageStore (total data usage)
│   ├─ History               CsvResultHistoryStore (history of results, one CSV file per day)
│   ├─ Network               NetworkInterfaceDataUsageMeter (data sent and received by the adapter)
│   ├─ AutoStart             WindowsAutoStartService (HKCU\Run)
│   └─ Power                 WindowsPowerEventSource (SystemEvents) / WindowsSleepInhibitor (prevents sleep while running)
├─ TetherKeeper              .NET MAUI UI (Views / ViewModels / Platforms)
│   ├─ Services              AppInfoProvider (version and other information) / AppPaths / AppPackage, etc.
│   ├─ Resources/Strings     AppStrings.resx (English) / AppStrings.ja.resx (Japanese): text for the screens
│   ├─ Platforms/Windows     Tray, single instance, connected networks (WindowsNetworkMonitor), Package.appxmanifest and the display name resources (Strings)
│   └─ ThirdPartyNotices.txt License notices opened by "License information"
├─ TetherKeeper.Mac          macOS version (in development): an AppKit menu bar app. Build it on a Mac with TetherKeeper.Mac.slnx
│   ├─ Services              MacAppPaths / MacAutoStartService (login items) / MacNotifier (Notification Center)
│   └─ Strings               MacStrings.resx (English) / MacStrings.ja.resx (Japanese): text used only on macOS (the rest is shared from AppStrings)
├─ KeepAlive.Tests           xUnit unit tests
├─ tools/notices             Script that creates ThirdPartyNotices.txt
├─ docs/UserGuide.md         User guide (Japanese version: UserGuide_ja.md)
├─ docs/PrivacyPolicy.md     Privacy policy
├─ docs/BuildGuide.md        App names, versions, and license notices (in Japanese)
├─ docs/StoreListing.md      Content to enter in Partner Center for the Microsoft Store submission (in Japanese, with English listing text)
├─ docs/MacBuildGuide.md     Building the macOS version and submitting it to the Mac App Store (in Japanese)
├─ docs/設計判断.md           Design decisions (in Japanese)
└─ docs/今後の機能候補.md     Feature ideas (in Japanese)
```

The background of the design is described in [docs/設計判断.md](docs/設計判断.md) (in Japanese).
How to handle the app names (Japanese and English), raise the version, and update the license notices is described in [docs/BuildGuide.md](docs/BuildGuide.md) (in Japanese).

## Adding a Method

1. Add a value to `KeepAliveMethod`.
2. Implement `IKeepAliveStrategy`. Return failures as `KeepAliveResult.Failed` instead of throwing exceptions.
3. Register it in `AddKeepAliveCore()`.
4. Add a display name to `SettingsViewModel.Methods`.
