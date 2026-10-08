using KeepAlive.Core.Interfaces;
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
/// </list>
/// </summary>
internal sealed class TrayController
{
    public const string StartInTrayArgument = "--tray";

    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly AppWindow _appWindow;
    private readonly IKeepAliveWorker _worker;
    private readonly AppExitService _exitService;
    private readonly DispatcherQueue _dispatcher;
    private readonly NotifyIcon _notifyIcon;
    private bool _hiddenNoticeShown;
    private bool _startInTray;

    public TrayController(Microsoft.UI.Xaml.Window window, IKeepAliveWorker worker, AppExitService exitService)
    {
        _window = window;
        _appWindow = window.AppWindow;
        _worker = worker;
        _exitService = exitService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // appicon.ico は MauiIcon からビルド時に生成され、exe と同じフォルダに出力される
        var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
        _notifyIcon = new NotifyIcon(iconPath, BuildToolTip())
        {
            MenuProvider = BuildMenu,
        };
        _notifyIcon.Click += (_, _) => ShowWindow();

        _appWindow.Closing += OnClosing;

        // Worker のイベントはバックグラウンドスレッドから来るため UI スレッドで処理する
        _worker.StateChanged += (_, _) => _dispatcher.TryEnqueue(UpdateToolTip);
        _worker.ResultReceived += (_, _) => _dispatcher.TryEnqueue(UpdateToolTip);
        _exitService.Exiting += (_, _) => _notifyIcon.Dispose();
        SingleInstance.ListenForActivation(() => _dispatcher.TryEnqueue(ShowWindow));

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
                "TetherKeeper はタスクトレイで動作中です",
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

    private async void ToggleWorker()
    {
        if (_worker.IsRunning)
            await _worker.StopAsync();
        else
            _worker.Start();
    }

    private void UpdateToolTip() => _notifyIcon.ToolTip = BuildToolTip();

    private string BuildToolTip()
    {
        if (!_worker.IsRunning)
            return "TetherKeeper（停止中）";

        var failures = _worker.Statistics.ConsecutiveFailures;
        return failures > 0
            ? $"TetherKeeper（動作中・{failures} 回連続失敗）"
            : "TetherKeeper（動作中）";
    }
}
