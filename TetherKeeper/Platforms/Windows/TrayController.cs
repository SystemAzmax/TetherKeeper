using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using TetherKeeper.Services;

namespace TetherKeeper.WinUI;

/// <summary>
/// タスクトレイ常駐の制御。
/// <list type="bullet">
/// <item>× ボタン (Alt+F4 含む) → ウィンドウを隠してトレイに格納</item>
/// <item>トレイアイコンをクリック → ウィンドウを再表示</item>
/// <item>トレイの右クリックメニュー → 上部に今の状態。表示 / 開始・停止 / 今すぐ実行 / 自動停止の予約 / 終了</item>
/// <item>ログイン時の自動起動 (起動引数 --tray、または MSIX の StartupTask による起動) → ウィンドウを出さずにトレイに格納した状態で起動</item>
/// <item>通信の失敗が続いたとき・そこから復旧したとき → 通知を出す。失敗が続いている間はアイコンに赤い印を付ける</item>
/// <item>指定したネットワークから切断して停止したとき → 通知を出す</item>
/// </list>
/// </summary>
internal sealed class TrayController
{
    public const string StartInTrayArgument = "--tray";

    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly AppWindow _appWindow;
    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly AutoStopScheduler _autoStop;
    private readonly AppExitService _exitService;
    private readonly DispatcherQueue _dispatcher;
    private readonly NotifyIcon _notifyIcon;
    private readonly FailureAlertTracker _failureAlerts = new(); // UI スレッドだけで使う
    private bool _hiddenNoticeShown;
    private bool _startInTray;

    public TrayController(
        Microsoft.UI.Xaml.Window window,
        IKeepAliveWorker worker,
        ISettingsService settings,
        NetworkAutoControl networkAutoControl,
        AutoStopScheduler autoStop,
        AppExitService exitService)
    {
        _autoStop = autoStop;
        _window = window;
        _appWindow = window.AppWindow;
        _worker = worker;
        _settings = settings;
        _exitService = exitService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // appicon.ico は MauiIcon からビルド時に生成され、exe と同じフォルダに出力される
        var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
        _notifyIcon = new NotifyIcon(iconPath, BuildToolTip())
        {
            MenuProvider = BuildMenu,
        };
        _notifyIcon.Click += (_, _) => ShowWindow();
        // 失敗の通知などをクリックしたら、ウィンドウを出して状態とログを見られるようにする
        _notifyIcon.BalloonClick += (_, _) => ShowWindow();

        _appWindow.Closing += OnClosing;

        // Worker のイベントはバックグラウンドスレッドから来るため UI スレッドで処理する
        // 停止のすぐ後に開始された場合も停止を取りこぼさないよう、状態はイベントの時点で取る
        _worker.StateChanged += (_, _) =>
        {
            var isRunning = _worker.IsRunning;
            _dispatcher.TryEnqueue(() => OnStateChanged(isRunning));
        };
        _worker.ResultReceived += (_, result) => _dispatcher.TryEnqueue(() => OnResultReceived(result));
        networkAutoControl.StoppedByDisconnection += (_, name) => _dispatcher.TryEnqueue(() => OnStoppedByDisconnection(name));
        _exitService.Exiting += (_, _) => _notifyIcon.Dispose();
        // UI スレッドで表示まで終えてから応答する (UI が固まっていれば応答せず、依頼元が利用者に知らせる)
        SingleInstance.ListenForActivation(() => _dispatcher.TryEnqueue(() =>
        {
            ShowWindow();
            SingleInstance.AcknowledgeActivation();
        }));

        _startInTray = IsLaunchedAtLogin();
        if (_startInTray)
            _window.Activated += OnFirstActivated;
    }

    /// <summary>ログイン時の自動起動で起動されたか。</summary>
    private static bool IsLaunchedAtLogin()
    {
        // 非パッケージ版: Run キーに登録した起動引数で判定
        if (Environment.GetCommandLineArgs().Contains(StartInTrayArgument, StringComparer.OrdinalIgnoreCase))
            return true;

        // MSIX 版: StartupTask から起動された場合は起動種別 (ActivationKind) が StartupTask になる
        try
        {
            return Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind
                == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        // MAUI がウィンドウを表示した直後に隠す (表示前に隠しても MAUI 側の Activate で再表示されるため)
        _window.Activated -= OnFirstActivated;
        if (_startInTray)
        {
            _startInTray = false;
            _appWindow.Hide();
        }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitService.IsExiting)
            return;

        // 閉じる代わりに隠す (タスクバーからも消える)
        args.Cancel = true;
        _appWindow.Hide();

        if (!_hiddenNoticeShown)
        {
            _hiddenNoticeShown = true;
            _notifyIcon.ShowBalloon(
                $"{AppDisplayName.JAPANESE}はタスクトレイで動作中です",
                "終了するには、トレイアイコンを右クリックして「終了」を選んでください。");
        }
    }

    private void ShowWindow()
    {
        _startInTray = false;
        _appWindow.Show();
        if (_appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        _window.Activate();
    }

    /// <summary>右クリックのたびに作り直す (状態の表示と項目の文言を、その時点のものにするため)。</summary>
    private IReadOnlyList<TrayMenuItem> BuildMenu()
    {
        var isRunning = _worker.IsRunning;
        var scheduledStop = _autoStop.ScheduledAt;
        var items = new List<TrayMenuItem>();

        // 上部: 今の状態 (選べない項目)
        items.AddRange(BuildStatusLines(isRunning, scheduledStop).Select(TrayMenuItem.Info));
        items.Add(TrayMenuItem.Separator);

        items.Add(new("ウィンドウを表示", ShowWindow, IsDefault: true));
        items.Add(new(isRunning ? "キープアライブを停止" : "キープアライブを開始", ToggleWorker));
        items.Add(new("今すぐ実行", _worker.TriggerNow, IsEnabled: isRunning));
        items.Add(new("自動停止を予約", null, Children:
        [
            new("1 時間後に停止", () => ScheduleAutoStop(TimeSpan.FromHours(1))),
            new("2 時間後に停止", () => ScheduleAutoStop(TimeSpan.FromHours(2))),
            TrayMenuItem.Separator,
            new("予約を解除", CancelAutoStop, IsEnabled: scheduledStop is not null),
        ]));
        items.Add(TrayMenuItem.Separator);
        items.Add(new("終了", () => _ = _exitService.ExitAsync()));
        return items;
    }

    /// <summary>メニュー上部に出す、今の状態 (状態、最後の結果、次回実行まで、自動停止の予定)。</summary>
    private IEnumerable<string> BuildStatusLines(bool isRunning, DateTimeOffset? scheduledStop)
    {
        var statistics = _worker.Statistics;
        yield return !isRunning ? "状態: 停止中"
            : statistics.ConsecutiveFailures > 0 ? $"状態: 動作中（{statistics.ConsecutiveFailures} 回連続失敗）"
            : "状態: 動作中";

        if (statistics.LastResult is { } last)
            yield return $"最後の結果: {last.Timestamp.ToLocalTime():HH:mm:ss} {(last.Success ? "成功" : "失敗")}";

        if (isRunning && _worker.NextRunAt is { } next)
        {
            var remaining = next - DateTimeOffset.Now;
            yield return remaining > TimeSpan.Zero
                ? $"次回実行まで {Math.Ceiling(remaining.TotalSeconds):F0} 秒"
                : "実行中…";
        }

        if (scheduledStop is { } stopAt)
            yield return $"自動停止: {stopAt:MM/dd HH:mm} に停止予定";
    }

    // 以下の async void は、例外を外に出すとアプリが落ちるため、すべて受け止める
    private async void ScheduleAutoStop(TimeSpan after)
    {
        try
        {
            await _autoStop.ScheduleAfterAsync(after);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TrayController: 自動停止の予約に失敗しました: {ex}");
        }
    }

    private async void CancelAutoStop()
    {
        try
        {
            await _autoStop.CancelAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TrayController: 自動停止の解除に失敗しました: {ex}");
        }
    }

    private async void ToggleWorker()
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
            System.Diagnostics.Debug.WriteLine($"TrayController: 開始／停止に失敗しました: {ex}");
        }
    }

    private void OnStateChanged(bool isRunning)
    {
        // 停止したら失敗の状態を忘れる (停止中は通信しないため、次の開始時の成功を「復旧」と知らせない)
        if (!isRunning)
        {
            _failureAlerts.Reset();
            _notifyIcon.ShowsAlert = false;
        }

        UpdateToolTip();
    }

    private void OnResultReceived(KeepAliveResult result)
    {
        var settings = _settings.Current;
        var alert = _failureAlerts.Add(result, settings.FailureThreshold);
        // 印は通知を切っていても付ける (通知を出さない設定でも、アイコンで気づけるようにする)
        _notifyIcon.ShowsAlert = _failureAlerts.IsFailing;
        UpdateToolTip();

        if (alert is null || !settings.NotifyOnFailure)
            return;

        _notifyIcon.ShowBalloon(
            alert.Title,
            alert.Message,
            alert.Kind == FailureAlertKind.Failing ? BalloonIcon.Warning : BalloonIcon.Info);
    }

    /// <summary>
    /// 指定したネットワークから切断して停止したことを知らせる。
    /// 停止すると失敗の通知は出なくなるため、切断に気づけるよう、失敗の通知と同じ設定で通知する。
    /// </summary>
    private void OnStoppedByDisconnection(string networkName)
    {
        if (!_settings.Current.NotifyOnFailure)
            return;

        _notifyIcon.ShowBalloon(
            "ネットワークから切断しました",
            $"「{networkName}」から切断したため、キープアライブを停止しました。",
            BalloonIcon.Warning);
    }

    private void UpdateToolTip() => _notifyIcon.ToolTip = BuildToolTip();

    private string BuildToolTip()
    {
        // アプリ名は表示言語に合わせて日本語名と英語名を切り替える
        var name = AppInfoProvider.GetProductName();
        if (!_worker.IsRunning)
            return $"{name}（停止中）";

        var failures = _worker.Statistics.ConsecutiveFailures;
        return failures > 0
            ? $"{name}（動作中・{failures} 回連続失敗）"
            : $"{name}（動作中）";
    }
}
