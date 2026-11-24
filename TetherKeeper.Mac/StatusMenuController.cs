using AppKit;
using Foundation;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using TetherKeeper.Mac.Services;

namespace TetherKeeper.Mac;

/// <summary>
/// メニューバーのアイコンとメニュー (Windows 版のタスクトレイに当たる)。
/// 状態と最後の結果を表示し、開始・停止、今すぐ実行、設定、ログのフォルダ、終了を操作できる。
/// 通信の失敗が続いたときと復旧したときに通知する。
/// </summary>
public sealed class StatusMenuController : NSObject
{
    private const string RunningSymbol = "antenna.radiowaves.left.and.right";
    private const string StoppedSymbol = "antenna.radiowaves.left.and.right.slash";
    private const string FailingSymbol = "exclamationmark.triangle.fill";

    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly IKeepAliveStrategyResolver _resolver;
    private readonly ILogService _log;
    private readonly MacNotifier _notifier;
    private readonly FailureAlertTracker _failureAlerts = new(); // メインスレッドだけで使う

    private readonly NSStatusItem _statusItem;
    private readonly NSMenuItem _statusLine;
    private readonly NSMenuItem _configurationLine;
    private readonly NSMenuItem _lastResultLine;
    private readonly NSMenuItem _toggleItem;
    private readonly NSMenuItem _runNowItem;
    private string? _currentSymbol;

    public StatusMenuController(
        IKeepAliveWorker worker,
        ISettingsService settings,
        IKeepAliveStrategyResolver resolver,
        ILogService log,
        MacNotifier notifier,
        Action showSettings)
    {
        _worker = worker;
        _settings = settings;
        _resolver = resolver;
        _log = log;
        _notifier = notifier;

        var appName = AppDisplayName.GetCurrent();
        _statusLine = new NSMenuItem { Enabled = false };
        _configurationLine = new NSMenuItem { Enabled = false };
        _lastResultLine = new NSMenuItem { Enabled = false };
        _toggleItem = new NSMenuItem(string.Empty, (_, _) => _ = ToggleAsync());
        _runNowItem = new NSMenuItem(AppStrings.Main_RunNow, (_, _) => _worker.TriggerNow());

        // 項目の有効・無効は自分で切り替える (状態の表示の行は押せないようにする)
        var menu = new NSMenu { AutoEnablesItems = false };
        menu.AddItem(_statusLine);
        menu.AddItem(_configurationLine);
        menu.AddItem(_lastResultLine);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(_toggleItem);
        menu.AddItem(_runNowItem);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem(MacStrings.Menu_Settings, ",", (_, _) => showSettings()));
        menu.AddItem(new NSMenuItem(MacStrings.Menu_OpenLogFolder, (_, _) => OpenLogFolder()));
        menu.AddItem(new NSMenuItem(Text.Format(AppStrings.About_Title, appName), (_, _) => ShowAbout()));
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem(Text.Format(MacStrings.Menu_Quit, appName), "q",
            (_, _) => NSApplication.SharedApplication.Terminate(this)));

        _statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
        _statusItem.Menu = menu;

        // Worker と設定のイベントはバックグラウンドのスレッドから来るため、メインスレッドに移す
        _worker.StateChanged += (_, _) => BeginInvokeOnMainThread(OnStateChanged);
        _worker.ResultReceived += (_, result) => BeginInvokeOnMainThread(() => OnResultReceived(result));
        _settings.SettingsChanged += (_, _) => BeginInvokeOnMainThread(Refresh);

        Refresh();
    }

    /// <summary>メニューとアイコンを、今の状態に合わせる。</summary>
    public void Refresh()
    {
        var isRunning = _worker.IsRunning;
        var statistics = _worker.Statistics;

        var status = !isRunning ? AppStrings.Status_Stopped
            : statistics.ConsecutiveFailures > 0 ? Text.Format(AppStrings.Status_RunningWithFailures, statistics.ConsecutiveFailures)
            : AppStrings.Status_Running;
        _statusLine.Title = Text.Format(AppStrings.Tray_Status, status);

        var settings = _settings.Current;
        var target = settings.Method switch
        {
            KeepAliveMethod.Ping => settings.PingHost,
            KeepAliveMethod.HttpHead => settings.HttpUrl,
            KeepAliveMethod.TcpConnect => $"{settings.TcpHost}:{settings.TcpPort}",
            _ => string.Empty,
        };
        _configurationLine.Title = Text.Format(
            AppStrings.Config_Text, _resolver.Resolve(settings.Method).Name, target, settings.IntervalSeconds);

        if (statistics.LastResult is { } last)
        {
            _lastResultLine.Title = Text.Format(AppStrings.Tray_LastResult, last.Timestamp.ToLocalTime(), FormatOutcome(last));
            _lastResultLine.Hidden = false;
        }
        else
        {
            _lastResultLine.Hidden = true;
        }

        _toggleItem.Title = isRunning ? AppStrings.Tray_StopKeepAlive : AppStrings.Tray_StartKeepAlive;
        _runNowItem.Enabled = isRunning;

        UpdateIcon(isRunning, statistics.ConsecutiveFailures);
    }

    private void UpdateIcon(bool isRunning, int consecutiveFailures)
    {
        var name = AppDisplayName.GetCurrent();
        var button = _statusItem.Button;
        button.ToolTip = !isRunning ? Text.Format(AppStrings.Tray_TooltipStopped, name)
            : consecutiveFailures > 0 ? Text.Format(AppStrings.Tray_TooltipFailing, name, consecutiveFailures)
            : Text.Format(AppStrings.Tray_TooltipRunning, name);

        // 失敗が続いている印は、通知を切っていても付ける (Windows 版のトレイアイコンの赤い印と同じ)
        var symbol = !isRunning ? StoppedSymbol
            : _failureAlerts.IsFailing ? FailingSymbol
            : RunningSymbol;
        if (symbol == _currentSymbol)
            return;

        if (NSImage.GetSystemSymbol(symbol, button.ToolTip) is { } image)
        {
            // テンプレート画像にすると、メニューバーの明るさ (ライト・ダーク) に合わせて色が変わる
            image.Template = true;
            button.Image = image;
            button.Title = string.Empty;
        }
        else
        {
            button.Image = null;
            button.Title = "TK";
        }

        _currentSymbol = symbol;
    }

    private void OnStateChanged()
    {
        // 停止したら失敗の状態を忘れる (停止中は通信しないため、次の開始時の成功を「復旧」と知らせない)
        if (!_worker.IsRunning)
            _failureAlerts.Reset();

        Refresh();
    }

    private void OnResultReceived(KeepAliveResult result)
    {
        var settings = _settings.Current;
        var alert = _failureAlerts.Add(result, settings.FailureThreshold);
        Refresh();

        if (alert is not null && settings.NotifyOnFailure)
            _notifier.Show(alert.Title, alert.Message);
    }

    private async Task ToggleAsync()
    {
        try
        {
            if (_worker.IsRunning)
                await _worker.StopAsync();
            else
                _worker.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StatusMenuController: 開始／停止に失敗しました: {ex}");
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(MacAppPaths.LogDirectory);
            NSWorkspace.SharedWorkspace.OpenUrl(NSUrl.FromFilename(MacAppPaths.LogDirectory));
        }
        catch (Exception ex)
        {
            _log.Error(Text.Format(AppStrings.Log_OpenFolderFailed, ex.Message));
        }
    }

    private void ShowAbout()
    {
        // バージョンと著作権は Info.plist (CFBundleShortVersionString、NSHumanReadableCopyright) から表示される
        AppActivation.BringToFront();
        NSApplication.SharedApplication.OrderFrontStandardAboutPanel(this);
    }

    /// <summary>結果の成否 (予備の通信方式で成功した場合は、その方式も出す)。Windows 版の MainViewModel.FormatOutcome と同じ。</summary>
    private static string FormatOutcome(KeepAliveResult result) =>
        !result.Success ? AppStrings.Outcome_Failed
        : result.UsedFallback ? Text.Format(AppStrings.Outcome_SucceededWithFallback, result.StrategyName)
        : AppStrings.Outcome_Succeeded;
}
