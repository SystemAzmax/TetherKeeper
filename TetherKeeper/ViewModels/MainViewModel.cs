using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using TetherKeeper.Services;

namespace TetherKeeper.ViewModels;

/// <summary>ログ一覧の 1 行。</summary>
/// <summary>ログ一覧の 1 行。<see cref="Entry"/> は重複表示の判定に使う。</summary>
public sealed record LogLine(LogEntry Entry, string Text, Color TextColor);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxVisibleLogs = 300;

    private static readonly Color RunningColor = Color.FromArgb("#43A047");
    private static readonly Color FailingColor = Color.FromArgb("#E53935");
    private static readonly Color StoppedColor = Color.FromArgb("#9E9E9E");
    private static readonly Color WarningColor = Color.FromArgb("#FB8C00");

    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly IDataUsageTracker _dataUsage;
    private readonly ILogService _log;
    private readonly IKeepAliveStrategyResolver _resolver;
    private readonly AppExitService _exitService;
    private bool _initialized;

    public MainViewModel(
        IKeepAliveWorker worker,
        ISettingsService settings,
        IDataUsageTracker dataUsage,
        ILogService log,
        IKeepAliveStrategyResolver resolver,
        AppExitService exitService)
    {
        _worker = worker;
        _settings = settings;
        _dataUsage = dataUsage;
        _log = log;
        _resolver = resolver;
        _exitService = exitService;

        _worker.StateChanged += OnWorkerChanged;
        _worker.ResultReceived += OnResultReceived;
        _settings.SettingsChanged += OnSettingsChanged;
        _dataUsage.Changed += OnDataUsageChanged;
        _log.EntryAdded += OnLogEntryAdded;
        _log.Cleared += OnLogCleared;

        // 初期値は次の正時
        var defaultStopAt = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 1);
        AutoStopDate = defaultStopAt.Date;
        AutoStopTime = defaultStopAt.TimeOfDay;

        RefreshAll();
        LoadInitialLogs();
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

    /// <summary>自動停止の日付 (ピッカーの入力値)。</summary>
    [ObservableProperty]
    public partial DateTime? AutoStopDate { get; set; }

    /// <summary>自動停止の時刻 (ピッカーの入力値)。</summary>
    [ObservableProperty]
    public partial TimeSpan? AutoStopTime { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditAutoStop))]
    public partial bool IsAutoStopScheduled { get; set; }

    public bool CanEditAutoStop => !IsAutoStopScheduled;

    [ObservableProperty]
    public partial string AutoStopButtonText { get; set; } = "予約";

    [ObservableProperty]
    public partial string AutoStopText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutoStopError))]
    public partial string AutoStopError { get; set; } = string.Empty;

    public bool HasAutoStopError => AutoStopError.Length > 0;

    /// <summary>通信量の累計と上限 (例: 12.3 MB / 500 MB)。</summary>
    [ObservableProperty]
    public partial string DataUsageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DataUsageNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDataLimitReached { get; set; }

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
        await _dataUsage.LoadAsync();
        RefreshAll();

        if (settings.StartOnLaunch)
            _worker.Start();
    }

    /// <summary>次回実行までのカウントダウン表示を更新する (UI タイマーから毎秒呼ぶ)。</summary>
    public void UpdateCountdown()
    {
        // 停止中に予約日時を過ぎた場合なども表示に反映する
        RefreshAutoStop();

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

    /// <summary>自動停止を予約する。予約済みなら解除する。</summary>
    [RelayCommand]
    private async Task ToggleAutoStopAsync()
    {
        AutoStopError = string.Empty;
        DateTimeOffset? stopAt = null;

        if (!IsAutoStopScheduled)
        {
            if (AutoStopDate is not { } date || AutoStopTime is not { } time)
            {
                AutoStopError = "自動停止の日付と時刻を指定してください。";
                return;
            }

            stopAt = new DateTimeOffset(DateTime.SpecifyKind(date.Date + time, DateTimeKind.Local));
            if (stopAt <= DateTimeOffset.Now)
            {
                AutoStopError = "現在より後の日時を指定してください。";
                return;
            }
        }

        var errors = await _settings.UpdateAsync(current => current with { AutoStopAt = stopAt });
        if (errors.Count > 0)
        {
            AutoStopError = string.Join(Environment.NewLine, errors);
            return;
        }

        _log.Info(stopAt is { } at
            ? $"自動停止を {at:yyyy/MM/dd HH:mm} に予約しました。"
            : "自動停止の予約を解除しました。");
        RefreshAutoStop();
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
    private Task ResetDataUsageAsync() => _dataUsage.ResetAsync();

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
        _dataUsage.Changed -= OnDataUsageChanged;
        _log.EntryAdded -= OnLogEntryAdded;
        _log.Cleared -= OnLogCleared;
    }

    // Worker / Log のイベントはバックグラウンドスレッドから来るため UI スレッドへ移す
    private void OnWorkerChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(RefreshAll);

    private void OnResultReceived(object? sender, KeepAliveResult e) => MainThread.BeginInvokeOnMainThread(RefreshAll);

    private void OnSettingsChanged(object? sender, AppSettings e) => MainThread.BeginInvokeOnMainThread(RefreshAll);

    private void OnDataUsageChanged(object? sender, DataUsage e) => MainThread.BeginInvokeOnMainThread(RefreshDataUsage);

    private void OnLogEntryAdded(object? sender, LogEntry entry) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // 初期表示の読み込みと追加通知が重なった場合に、同じログを二重に並べない
            if (Logs.Any(line => ReferenceEquals(line.Entry, entry)))
                return;

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
        RefreshDataUsage();
        UpdateCountdown();
    }

    /// <summary>
    /// 画面を作る前に出力されたログを表示する (以降のログは EntryAdded で追加する)。
    /// 購読より後に読み込むため、購読から読み込みまでの間に出たログは両方の経路で届くが、
    /// <see cref="OnLogEntryAdded"/> 側で重複を除く。
    /// </summary>
    private void LoadInitialLogs()
    {
        foreach (var entry in _log.GetEntries().Reverse().Take(MaxVisibleLogs))
            Logs.Add(ToLogLine(entry));
    }

    private void RefreshAutoStop()
    {
        // 過ぎた日時 (停止中に迎えた予約など) は未予約として扱う
        var stopAt = _settings.Current.AutoStopAt is { } at && at > DateTimeOffset.Now ? at.ToLocalTime() : (DateTimeOffset?)null;
        IsAutoStopScheduled = stopAt is not null;
        AutoStopButtonText = IsAutoStopScheduled ? "解除" : "予約";

        if (stopAt is { } scheduled)
        {
            AutoStopDate = scheduled.Date;
            AutoStopTime = new TimeSpan(scheduled.Hour, scheduled.Minute, 0);
            AutoStopText = IsRunning
                ? $"{scheduled:yyyy/MM/dd HH:mm} にキープアライブを停止します"
                : $"{scheduled:yyyy/MM/dd HH:mm} に停止予定（開始すると有効になります）";
        }
        else
        {
            AutoStopText = "指定した日時にキープアライブを停止します（アプリは終了しません）";
        }
    }

    private void RefreshDataUsage()
    {
        var usage = _dataUsage.Current;
        var settings = _settings.Current;
        var total = DataUsage.Format(usage.TotalBytes);
        DataUsageText = settings.DataLimitBytes is null
            ? $"{total}（上限なし）"
            : $"{total} / {settings.DataLimitMegabytes:N0} MB";
        IsDataLimitReached = settings.DataLimitBytes is { } limit && usage.TotalBytes >= limit;
        DataUsageNote = usage.Since is { } since
            ? $"{since.ToLocalTime():yyyy/MM/dd HH:mm} からの累計（キープアライブの動作中に計測。ほかのアプリの通信も含む）"
            : "キープアライブの動作中に、使用中のネットワークの送受信量を数えます（ほかのアプリの通信も含む）";
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
        entry,
        entry.ToString(),
        entry.Level switch
        {
            LogLevelKind.Error => FailingColor,
            LogLevelKind.Warning => WarningColor,
            _ => Colors.Gray,
        });
}
