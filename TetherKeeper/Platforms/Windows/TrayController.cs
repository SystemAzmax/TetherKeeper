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
/// <item>トレイの右クリックメニュー → 表示 / 開始・停止 / 終了</item>
/// <item>ログイン時の自動起動 (起動引数 --tray、または MSIX の StartupTask による起動) → ウィンドウを出さずにトレイに格納した状態で起動</item>
/// <item>通信の失敗が続いたとき・そこから復旧したとき → 通知を出す。失敗が続いている間はアイコンに赤い印を付ける</item>
/// </list>
/// </summary>
internal sealed class TrayController
{
    public const string StartInTrayArgument = "--tray";

    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly AppWindow _appWindow;
    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly AppExitService _exitService;
    private readonly DispatcherQueue _dispatcher;
    private readonly NotifyIcon _notifyIcon;
    private readonly FailureAlertTracker _failureAlerts = new(); // UI スレッドだけで使う
    private bool _hiddenNoticeShown;
    private bool _startInTray;

    public TrayController(
        Microsoft.UI.Xaml.Window window, IKeepAliveWorker worker, ISettingsService settings, AppExitService exitService)
    {
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

    private IReadOnlyList<TrayMenuItem> BuildMenu() =>
    [
        new("ウィンドウを表示", ShowWindow, IsDefault: true),
        new(_worker.IsRunning ? "キープアライブを停止" : "キープアライブを開始", ToggleWorker),
        TrayMenuItem.Separator,
        new("終了", () => _ = _exitService.ExitAsync()),
    ];

    // async void のため、例外を外に出すとアプリが落ちる
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
