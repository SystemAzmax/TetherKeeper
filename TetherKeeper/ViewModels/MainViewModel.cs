using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using TetherKeeper.Services;

namespace TetherKeeper.ViewModels;

/// <summary>ログ一覧の 1 行。</summary>
public sealed record LogLine(string Text, Color TextColor);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxVisibleLogs = 300;

    private static readonly Color RunningColor = Color.FromArgb("#43A047");
    private static readonly Color FailingColor = Color.FromArgb("#E53935");
    private static readonly Color StoppedColor = Color.FromArgb("#9E9E9E");
    private static readonly Color WarningColor = Color.FromArgb("#FB8C00");

    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly IKeepAliveStrategyResolver _resolver;
    private readonly AppExitService _exitService;
    private bool _initialized;

    public MainViewModel(
        IKeepAliveWorker worker,
        ISettingsService settings,
        ILogService log,
        IKeepAliveStrategyResolver resolver,
        AppExitService exitService)
    {
        _worker = worker;
        _settings = settings;
        _log = log;
        _resolver = resolver;
        _exitService = exitService;

        _worker.StateChanged += OnWorkerChanged;
        _worker.ResultReceived += OnResultReceived;
        _settings.SettingsChanged += OnSettingsChanged;
        _log.EntryAdded += OnLogEntryAdded;
        _log.Cleared += OnLogCleared;

        RefreshAll();
    }

    public ObservableCollection<LogLine> Logs { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunNowCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "停止中";

    [ObservableProperty]
    public partial Color StatusColor { get; set; } = StoppedColor;

    [ObservableProperty]
    public partial string ToggleButtonText { get; set; } = "開始";

    [ObservableProperty]
    public partial string ConfigurationText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NextRunText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial int SuccessCount { get; set; }

    [ObservableProperty]
    public partial int FailureCount { get; set; }

    [ObservableProperty]
    public partial int ConsecutiveFailures { get; set; }

    [ObservableProperty]
    public partial string LastResultText { get; set; } = "まだ実行していません";

    /// <summary>
    /// 初回表示時に 1 度だけ呼ぶ。設定を読み込み、必要なら自動開始する。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        _log.Info($"TetherKeeper {AppInfo.Current.VersionString} を起動しました。");
        var settings = await _settings.LoadAsync();
        RefreshAll();

        if (settings.StartOnLaunch)
            _worker.Start();
    }

    /// <summary>次回実行までのカウントダウン表示を更新する (UI タイマーから毎秒呼ぶ)。</summary>
    public void UpdateCountdown()
    {
        if (_worker.NextRunAt is not { } next)
        {
            NextRunText = IsRunning ? "実行中…" : string.Empty;
            return;
        }

        var remaining = next - DateTimeOffset.Now;
        NextRunText = remaining > TimeSpan.Zero
            ? $"次回実行まで {Math.Ceiling(remaining.TotalSeconds):F0} 秒"
            : "実行中…";
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (_worker.IsRunning)
            await _worker.StopAsync();
        else
            _worker.Start();
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void RunNow() => _worker.TriggerNow();

    [RelayCommand]
    private void ResetStatistics()
    {
        _worker.ResetStatistics();
        RefreshStatistics();
    }

    [RelayCommand]
    private void ClearLog() => _log.Clear();

    /// <summary>ログファイルの保存フォルダをエクスプローラーで開く。</summary>
    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogDirectory);
            Process.Start(new ProcessStartInfo { FileName = AppPaths.LogDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error($"ログフォルダを開けませんでした: {ex.Message}");
        }
    }

    /// <summary>アプリを終了する (Windows では × ボタンはトレイ格納のため、終了はこちら)。</summary>
    [RelayCommand]
    private Task ExitAsync() => _exitService.ExitAsync();

    public void Dispose()
    {
        _worker.StateChanged -= OnWorkerChanged;
        _worker.ResultReceived -= OnResultReceived;
        _settings.SettingsChanged -= OnSettingsChanged;
        _log.EntryAdded -= OnLogEntryAdded;
        _log.Cleared -= OnLogCleared;
    }

    // Worker / Log のイベントはバックグラウンドスレッドから来るため UI スレッドへ移す
    private void OnWorkerChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(RefreshAll);

    private void OnResultReceived(object? sender, KeepAliveResult e) => MainThread.BeginInvokeOnMainThread(RefreshAll);

    private void OnSettingsChanged(object? sender, AppSettings e) => MainThread.BeginInvokeOnMainThread(RefreshAll);

    private void OnLogEntryAdded(object? sender, LogEntry entry) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Logs.Insert(0, ToLogLine(entry));
            while (Logs.Count > MaxVisibleLogs)
                Logs.RemoveAt(Logs.Count - 1);
        });

    private void OnLogCleared(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(Logs.Clear);

    private void RefreshAll()
    {
        IsRunning = _worker.IsRunning;
        ToggleButtonText = IsRunning ? "停止" : "開始";

        var settings = _settings.Current;
        var strategyName = _resolver.Resolve(settings.Method).Name;
        var target = settings.Method switch
        {
            KeepAliveMethod.Ping => settings.PingHost,
            KeepAliveMethod.HttpHead => settings.HttpUrl,
            KeepAliveMethod.TcpConnect => $"{settings.TcpHost}:{settings.TcpPort}",
            _ => string.Empty,
        };
        ConfigurationText = $"{strategyName} → {target}（{settings.IntervalSeconds} 秒間隔）";

        RefreshStatistics();
        UpdateCountdown();

        if (Logs.Count == 0)
        {
            foreach (var entry in _log.GetEntries().Reverse().Take(MaxVisibleLogs))
                Logs.Add(ToLogLine(entry));
        }
    }

    private void RefreshStatistics()
    {
        var stats = _worker.Statistics;
        TotalCount = stats.TotalCount;
        SuccessCount = stats.SuccessCount;
        FailureCount = stats.FailureCount;
        ConsecutiveFailures = stats.ConsecutiveFailures;

        LastResultText = stats.LastResult is { } last
            ? $"{last.Timestamp:HH:mm:ss} {(last.Success ? "成功" : "失敗")} — {last.Message}"
            : "まだ実行していません";

        if (!IsRunning)
        {
            StatusText = "停止中";
            StatusColor = StoppedColor;
        }
        else if (stats.ConsecutiveFailures > 0)
        {
            StatusText = $"動作中（{stats.ConsecutiveFailures} 回連続失敗）";
            StatusColor = FailingColor;
        }
        else
        {
            StatusText = "動作中";
            StatusColor = RunningColor;
        }
    }

    private static LogLine ToLogLine(LogEntry entry) => new(
        entry.ToString(),
        entry.Level switch
        {
            LogLevelKind.Error => FailingColor,
            LogLevelKind.Warning => WarningColor,
            _ => Colors.Gray,
        });
}
