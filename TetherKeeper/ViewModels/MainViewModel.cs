using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
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
    private readonly NetworkAutoControl _networkAutoControl;
    private readonly AutoStopScheduler _autoStop;
    private bool _initialized;

    public MainViewModel(
        IKeepAliveWorker worker,
        ISettingsService settings,
        IDataUsageTracker dataUsage,
        ILogService log,
        IKeepAliveStrategyResolver resolver,
        AppExitService exitService,
        NetworkAutoControl networkAutoControl,
        AutoStopScheduler autoStop)
    {
        _autoStop = autoStop;
        _worker = worker;
        _settings = settings;
        _dataUsage = dataUsage;
        _log = log;
        _resolver = resolver;
        _exitService = exitService;
        _networkAutoControl = networkAutoControl;

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
    public partial string StatusText { get; set; } = AppStrings.Status_Stopped;

    [ObservableProperty]
    public partial Color StatusColor { get; set; } = StoppedColor;

    [ObservableProperty]
    public partial string ToggleButtonText { get; set; } = AppStrings.Button_Start;

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
    public partial string AutoStopButtonText { get; set; } = AppStrings.Button_Schedule;

    [ObservableProperty]
    public partial string AutoStopText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAutoStopError))]
    public partial string AutoStopError { get; set; } = string.Empty;

    public bool HasAutoStopError => AutoStopError.Length > 0;

    [ObservableProperty]
    public partial bool IsWeeklyStopEnabled { get; set; }

    /// <summary>毎週の自動停止の予定 (例: 毎週の自動停止: 平日 18:00（次回 11/24（火）18:00）)。</summary>
    [ObservableProperty]
    public partial string WeeklyStopText { get; set; } = string.Empty;

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
    public partial string LastResultText { get; set; } = AppStrings.Result_NotRunYet;

    /// <summary>予備の通信方式で成功した回数 (0 回のときは空)。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFallbackSuccess))]
    public partial string FallbackSuccessText { get; set; } = string.Empty;

    public bool HasFallbackSuccess => FallbackSuccessText.Length > 0;

    /// <summary>
    /// 初回表示時に 1 度だけ呼ぶ。設定を読み込み、必要なら自動開始する。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        var (version, isPackaged) = AppInfoProvider.GetVersion();
        _log.Info(Text.Format(
            AppStrings.App_Started,
            AppDisplayName.GetCurrent(),
            AppAboutInfo.FormatVersion(version),
            isPackaged ? CoreStrings.About_StoreEdition : CoreStrings.About_ExeEdition));
        var settings = await _settings.LoadAsync();
        await _dataUsage.LoadAsync();
        RefreshAll();

        if (settings.StartOnLaunch)
            _worker.Start();

        // 指定したネットワークにつながっていれば開始し、以降は接続の変化に合わせて開始・停止する
        await _networkAutoControl.ActivateAsync();
    }

    /// <summary>次回実行までのカウントダウン表示を更新する (UI タイマーから毎秒呼ぶ)。</summary>
    public void UpdateCountdown()
    {
        // 停止中に予約日時を過ぎた場合なども表示に反映する
        RefreshAutoStop();

        if (_worker.NextRunAt is not { } next)
        {
            NextRunText = IsRunning ? AppStrings.Next_Running : string.Empty;
            return;
        }

        var remaining = next - DateTimeOffset.Now;
        NextRunText = remaining > TimeSpan.Zero
            ? Text.Format(AppStrings.Next_Seconds, Math.Ceiling(remaining.TotalSeconds))
            : AppStrings.Next_Running;
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
                AutoStopError = AppStrings.AutoStop_DateTimeRequired;
                return;
            }

            stopAt = new DateTimeOffset(DateTime.SpecifyKind(date.Date + time, DateTimeKind.Local));
        }

        ShowAutoStopResult(stopAt is { } at
            ? await _autoStop.ScheduleAtAsync(at)
            : await _autoStop.CancelAsync());
    }

    /// <summary>今から指定した時間 (時間単位。コマンドの引数) の後に、自動停止を予約する。</summary>
    [RelayCommand]
    private async Task ScheduleAutoStopAfterAsync(string hours)
    {
        AutoStopError = string.Empty;
        if (!int.TryParse(hours, out var value) || value <= 0)
            return;

        ShowAutoStopResult(await _autoStop.ScheduleAfterAsync(TimeSpan.FromHours(value)));
    }

    private void ShowAutoStopResult(IReadOnlyList<string> errors)
    {
        AutoStopError = string.Join(Environment.NewLine, errors);
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
            _log.Error(Text.Format(AppStrings.Log_OpenFolderFailed, ex.Message));
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
        ToggleButtonText = IsRunning ? AppStrings.Button_Stop : AppStrings.Button_Start;

        var settings = _settings.Current;
        var strategyName = _resolver.Resolve(settings.Method).Name;
        var target = settings.Method switch
        {
            KeepAliveMethod.Ping => settings.PingHost,
            KeepAliveMethod.HttpHead => settings.HttpUrl,
            KeepAliveMethod.TcpConnect => $"{settings.TcpHost}:{settings.TcpPort}",
            _ => string.Empty,
        };
        ConfigurationText = Text.Format(AppStrings.Config_Text, strategyName, target, settings.IntervalSeconds);

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
        var stopAt = _autoStop.ScheduledAt;
        IsAutoStopScheduled = stopAt is not null;
        AutoStopButtonText = IsAutoStopScheduled ? AppStrings.Button_Cancel : AppStrings.Button_Schedule;

        if (stopAt is { } scheduled)
        {
            AutoStopDate = scheduled.Date;
            AutoStopTime = new TimeSpan(scheduled.Hour, scheduled.Minute, 0);
            AutoStopText = IsRunning
                ? Text.Format(AppStrings.AutoStop_WillStop, scheduled)
                : Text.Format(AppStrings.AutoStop_WillStopWhenStarted, scheduled);
        }
        else
        {
            AutoStopText = _settings.Current.ExitAppOnAutoStop
                ? AppStrings.AutoStop_DescriptionExit
                : AppStrings.AutoStop_Description;
        }

        RefreshWeeklyStop();
    }

    /// <summary>毎週の自動停止の予定を表示する (設定は設定画面で行う)。</summary>
    private void RefreshWeeklyStop()
    {
        var settings = _settings.Current;
        IsWeeklyStopEnabled = settings.WeeklyStopEnabled;
        if (!settings.WeeklyStopEnabled)
        {
            WeeklyStopText = string.Empty;
            return;
        }

        var next = WeeklySchedule.GetNextOccurrence(
            settings.WeeklyStopDays, settings.WeeklyStopTime, DateTimeOffset.Now, TimeZoneInfo.Local);
        var nextText = next is { } at
            ? Text.Format(AppStrings.WeeklyStop_Next, at.ToLocalTime(), WeeklySchedule.GetDayName(at.ToLocalTime().DayOfWeek))
            : string.Empty;
        WeeklyStopText = Text.Format(
            AppStrings.WeeklyStop_Text, WeeklySchedule.Format(settings.WeeklyStopDays), $"{settings.WeeklyStopTime:HH\\:mm}", nextText);
    }

    private void RefreshDataUsage()
    {
        var usage = _dataUsage.Current;
        var settings = _settings.Current;
        var total = DataUsage.Format(usage.TotalBytes);
        DataUsageText = settings.DataLimitBytes is null
            ? Text.Format(AppStrings.DataUsage_NoLimit, total)
            : $"{total} / {settings.DataLimitMegabytes:N0} MB";
        IsDataLimitReached = settings.DataLimitBytes is { } limit && usage.TotalBytes >= limit;
        DataUsageNote = usage.Since is { } since
            ? Text.Format(AppStrings.DataUsage_Since, since.ToLocalTime())
            : AppStrings.DataUsage_Note;
    }

    private void RefreshStatistics()
    {
        var stats = _worker.Statistics;
        TotalCount = stats.TotalCount;
        SuccessCount = stats.SuccessCount;
        FailureCount = stats.FailureCount;
        ConsecutiveFailures = stats.ConsecutiveFailures;

        LastResultText = stats.LastResult is { } last
            ? $"{last.Timestamp:HH:mm:ss} {FormatOutcome(last)} — {last.Message}"
            : AppStrings.Result_NotRunYet;
        FallbackSuccessText = stats.FallbackSuccessCount > 0
            ? Text.Format(AppStrings.Stats_FallbackSuccesses, stats.FallbackSuccessCount)
            : string.Empty;

        if (!IsRunning)
        {
            StatusText = AppStrings.Status_Stopped;
            StatusColor = StoppedColor;
        }
        else if (stats.ConsecutiveFailures > 0)
        {
            StatusText = Text.Format(AppStrings.Status_RunningWithFailures, stats.ConsecutiveFailures);
            StatusColor = FailingColor;
        }
        else
        {
            StatusText = AppStrings.Status_Running;
            StatusColor = RunningColor;
        }
    }

    /// <summary>結果の成否 (予備の通信方式で成功した場合は、その方式も出す)。</summary>
    public static string FormatOutcome(KeepAliveResult result) =>
        !result.Success ? AppStrings.Outcome_Failed
        : result.UsedFallback ? Text.Format(AppStrings.Outcome_SucceededWithFallback, result.StrategyName)
        : AppStrings.Outcome_Succeeded;

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
