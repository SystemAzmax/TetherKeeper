using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;

namespace TetherKeeper.ViewModels;

public sealed record MethodOption(KeepAliveMethod Method, string DisplayName, string Description);

/// <summary>表示言語の選択肢。<see cref="Code"/> が空なら「Windows の表示言語に合わせる」。</summary>
public sealed record LanguageOption(string Code, string DisplayName);

/// <summary>予備の通信方式の選択肢。<see cref="Method"/> が null なら「使わない」。</summary>
public sealed record FallbackOption(KeepAliveMethod? Method, string DisplayName);

/// <summary>毎週の自動停止の曜日のチェックボックス 1 つ分。</summary>
public sealed partial class WeeklyDayOption(DayOfWeek day) : ObservableObject
{
    public DayOfWeek Day { get; } = day;

    public string Name { get; } = WeeklySchedule.GetDayName(day);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAutoStartService _autoStart;
    private readonly ILogService _log;
    private readonly ISleepInhibitor _sleepInhibitor;
    private readonly INetworkMonitor _networkMonitor;

    public SettingsViewModel(
        ISettingsService settings,
        IAutoStartService autoStart,
        ILogService log,
        ISleepInhibitor sleepInhibitor,
        INetworkMonitor networkMonitor)
    {
        _settings = settings;
        _autoStart = autoStart;
        _log = log;
        _sleepInhibitor = sleepInhibitor;
        _networkMonitor = networkMonitor;
        SelectedLanguage = LanguageOptions[0];
        SelectedFallback = FallbackOptions[0];
        SelectedMethod = Methods[0];
        Load(_settings.Current);
        _ = RefreshLaunchAtLoginAsync();
        _ = RefreshNetworksAsync();
    }

    public IReadOnlyList<MethodOption> Methods { get; } =
    [
        new(KeepAliveMethod.Ping, "Ping (ICMP)", AppStrings.Method_PingDescription),
        new(KeepAliveMethod.HttpHead, "HTTP HEAD", AppStrings.Method_HttpDescription),
        new(KeepAliveMethod.TcpConnect, "TCP Connect", AppStrings.Method_TcpDescription),
    ];

    /// <summary>
    /// 表示言語の選択肢。先頭は「Windows の表示言語に合わせる」、続いて用意している言語を、その言語自身の名前で並べる
    /// (今の表示言語が読めなくても、元の言語を選び直せるようにするため)。
    /// </summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new(string.Empty, AppStrings.Language_System),
        .. AppLanguages.Supported.Select(l => new LanguageOption(l.Code, l.NativeName)),
    ];

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    /// <summary>表示言語を変えて保存した。アプリを再起動すると切り替わる。</summary>
    public event EventHandler? LanguageChanged;

    public bool IsAutoStartSupported => _autoStart.IsSupported;

    public bool IsSleepPreventionSupported => _sleepInhibitor.IsSupported;

    public bool IsNetworkAutoControlSupported => _networkMonitor.IsSupported;

    [ObservableProperty]
    public partial bool AutoControlByNetwork { get; set; }

    /// <summary>自動で開始・停止するネットワークの名前。一覧から選ぶほか、つながっていないネットワークの名前を入力することもできる。</summary>
    [ObservableProperty]
    public partial string AutoControlNetworkName { get; set; } = string.Empty;

    /// <summary>今つながっているネットワークの一覧 (名前の選択肢)。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ConnectedNetwork> ConnectedNetworks { get; set; } = [];

    /// <summary>一覧で選んだネットワーク。選ぶと、その名前を <see cref="AutoControlNetworkName"/> に入れる。</summary>
    [ObservableProperty]
    public partial ConnectedNetwork? SelectedConnectedNetwork { get; set; }

    partial void OnSelectedConnectedNetworkChanged(ConnectedNetwork? value)
    {
        if (value is not null)
            AutoControlNetworkName = value.Name;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPing), nameof(IsHttpHead), nameof(IsTcpConnect), nameof(MethodDescription))]
    public partial MethodOption SelectedMethod { get; set; }

    /// <summary>予備の通信方式の選択肢 (先頭は「使わない」)。</summary>
    public IReadOnlyList<FallbackOption> FallbackOptions { get; } =
    [
        new(null, AppStrings.Fallback_None),
        new(KeepAliveMethod.Ping, "Ping (ICMP)"),
        new(KeepAliveMethod.HttpHead, "HTTP HEAD"),
        new(KeepAliveMethod.TcpConnect, "TCP Connect"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPing), nameof(IsHttpHead), nameof(IsTcpConnect))]
    public partial FallbackOption SelectedFallback { get; set; }

    partial void OnSelectedMethodChanged(MethodOption value)
    {
        // 通信方式と同じ方式は予備にできないため、同じになったら「使わない」に戻す
        if (SelectedFallback?.Method == value.Method)
            SelectedFallback = FallbackOptions[0];
        UpdateTrafficEstimate();
    }

    partial void OnIntervalSecondsChanged(string value) => UpdateTrafficEstimate();

    partial void OnHttpUrlChanged(string value) => UpdateTrafficEstimate();

    /// <summary>
    /// 入力中の通信方式と実行間隔での、通信量の目安 (例: 通信量の目安: 1 時間あたり 約 14〜19 KB（24 時間で 約 0.35〜0.46 MB）)。
    /// 実行間隔が正しくなければ空。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrafficEstimate))]
    public partial string TrafficEstimateText { get; set; } = string.Empty;

    public bool HasTrafficEstimate => TrafficEstimateText.Length > 0;

    private void UpdateTrafficEstimate()
    {
        if (SelectedMethod is null
            || !int.TryParse(IntervalSeconds?.Trim(), out var interval)
            || interval is < AppSettings.MinIntervalSeconds or > AppSettings.MaxIntervalSeconds
            || TrafficEstimate.PerHour(SelectedMethod.Method, interval, HttpUrl) is not { } perHour)
        {
            TrafficEstimateText = string.Empty;
            return;
        }

        TrafficEstimateText = Text.Format(AppStrings.Settings_TrafficEstimate, perHour.Format(), perHour.Multiply(24).Format());
    }

    [ObservableProperty]
    public partial string IntervalSeconds { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TimeoutMilliseconds { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PingHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HttpUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TcpHost { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TcpPort { get; set; } = string.Empty;

    /// <summary>通信量の上限 (MB)。空欄は 0 (上限なし) とみなす。</summary>
    [ObservableProperty]
    public partial string DataLimitMegabytes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool NotifyOnFailure { get; set; }

    [ObservableProperty]
    public partial string FailureThreshold { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool WeeklyStopEnabled { get; set; }

    [ObservableProperty]
    public partial TimeSpan? WeeklyStopTime { get; set; }

    /// <summary>毎週の自動停止の曜日 (月曜始まり)。</summary>
    public IReadOnlyList<WeeklyDayOption> WeeklyStopDays { get; } =
        [.. Enumerable.Range(1, 7).Select(i => new WeeklyDayOption((DayOfWeek)(i % 7)))];

    [ObservableProperty]
    public partial bool ExitAppOnAutoStop { get; set; }

    [ObservableProperty]
    public partial bool PreventSleepWhileRunning { get; set; }

    [ObservableProperty]
    public partial bool StartOnLaunch { get; set; }

    [ObservableProperty]
    public partial bool LaunchAtLogin { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool HasErrors => ErrorText.Length > 0;

    // 送信先の入力欄は、通信方式か予備の通信方式で使う方式の分だけ表示する
    public bool IsPing => UsesMethod(KeepAliveMethod.Ping);

    public bool IsHttpHead => UsesMethod(KeepAliveMethod.HttpHead);

    public bool IsTcpConnect => UsesMethod(KeepAliveMethod.TcpConnect);

    private bool UsesMethod(KeepAliveMethod method) =>
        SelectedMethod?.Method == method || SelectedFallback?.Method == method;

    public string MethodDescription => SelectedMethod.Description;

    /// <summary>保存済みの設定を画面に再読み込みする (未保存の編集は破棄)。</summary>
    public async Task ReloadAsync()
    {
        Load(_settings.Current);
        ErrorText = string.Empty;
        StatusMessage = string.Empty;
        await RefreshLaunchAtLoginAsync();
        await RefreshNetworksAsync();
    }

    /// <summary>今つながっているネットワークの一覧を取り直す。</summary>
    [RelayCommand]
    private async Task RefreshNetworksAsync()
    {
        if (!_networkMonitor.IsSupported)
            return;

        try
        {
            // 取得に少し時間がかかることがあるため、UI スレッドを止めない
            ConnectedNetworks = await Task.Run(_networkMonitor.GetConnectedNetworks);
            SelectedConnectedNetwork = null;
        }
        catch (Exception ex)
        {
            _log.Warning(Text.Format(AppStrings.Settings_NetworksLoadFailed, ex.Message));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        var parseErrors = new List<string>();
        var interval = ParseInt(IntervalSeconds, AppStrings.Label_Interval, parseErrors);
        var timeout = ParseInt(TimeoutMilliseconds, AppStrings.Label_Timeout, parseErrors);
        var port = ParseInt(TcpPort, AppStrings.Label_TcpPort, parseErrors);
        var dataLimit = string.IsNullOrWhiteSpace(DataLimitMegabytes)
            ? 0
            : ParseInt(DataLimitMegabytes, AppStrings.Label_DataLimit, parseErrors);
        var failureThreshold = ParseInt(FailureThreshold, AppStrings.Label_FailureThreshold, parseErrors);
        if (parseErrors.Count > 0)
        {
            ErrorText = string.Join(Environment.NewLine, parseErrors);
            return;
        }

        // この画面で扱う項目だけを、保存直前の最新の設定に上書きする
        // (自動停止日時などモニター画面側で変わる項目を、画面を開いた時点の古い値で戻さないため)
        var method = SelectedMethod.Method;
        var fallbackMethod = SelectedFallback.Method;
        var language = SelectedLanguage.Code;
        var previousLanguage = _settings.Current.Language;
        var pingHost = PingHost.Trim();
        var httpUrl = HttpUrl.Trim();
        var tcpHost = TcpHost.Trim();
        var notifyOnFailure = NotifyOnFailure;
        var preventSleep = PreventSleepWhileRunning;
        var weeklyStopEnabled = WeeklyStopEnabled;
        var weeklyStopTime = WeeklyStopTime is { } time
            ? new TimeOnly(time.Hours, time.Minutes)
            : new AppSettings().WeeklyStopTime;
        var weeklyStopDays = WeeklyStopDays
            .Where(d => d.IsSelected)
            .Aggregate(WeeklyDays.None, (days, d) => days | d.Day.ToWeeklyDays());
        var exitAppOnAutoStop = ExitAppOnAutoStop;
        var autoControlByNetwork = AutoControlByNetwork;
        var autoControlNetworkName = AutoControlNetworkName.Trim();
        var startOnLaunch = StartOnLaunch;
        var launchAtLogin = LaunchAtLogin;
        var errors = await _settings.UpdateAsync(current => current with
        {
            Language = language,
            Method = method,
            FallbackMethod = fallbackMethod,
            IntervalSeconds = interval,
            TimeoutMilliseconds = timeout,
            PingHost = pingHost,
            HttpUrl = httpUrl,
            TcpHost = tcpHost,
            TcpPort = port,
            DataLimitMegabytes = dataLimit,
            NotifyOnFailure = notifyOnFailure,
            FailureThreshold = failureThreshold,
            PreventSleepWhileRunning = preventSleep,
            WeeklyStopEnabled = weeklyStopEnabled,
            WeeklyStopTime = weeklyStopTime,
            WeeklyStopDays = weeklyStopDays,
            ExitAppOnAutoStop = exitAppOnAutoStop,
            AutoControlByNetwork = autoControlByNetwork,
            AutoControlNetworkName = autoControlNetworkName,
            StartOnLaunch = startOnLaunch,
            LaunchAtLogin = launchAtLogin,
        });
        if (errors.Count > 0)
        {
            ErrorText = string.Join(Environment.NewLine, errors);
            return;
        }

        ErrorText = string.Empty;
        if (!await TryApplyAutoStartAsync(LaunchAtLogin))
            return;

        StatusMessage = Text.Format(AppStrings.Settings_SavedAt, DateTime.Now);
        Load(_settings.Current);
        await RefreshLaunchAtLoginAsync();

        // 言語は再起動で切り替わるため、画面側で再起動するかを尋ねる
        if (_settings.Current.Language != previousLanguage)
            LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        Load(defaults);
        StatusMessage = AppStrings.Settings_DefaultsEntered;
    }

    private void Load(AppSettings settings)
    {
        SelectedLanguage = LanguageOptions.FirstOrDefault(l => l.Code == settings.Language) ?? LanguageOptions[0];
        SelectedMethod = Methods.FirstOrDefault(m => m.Method == settings.Method) ?? Methods[0];
        SelectedFallback = FallbackOptions.FirstOrDefault(f => f.Method == settings.FallbackMethod) ?? FallbackOptions[0];
        IntervalSeconds = settings.IntervalSeconds.ToString();
        TimeoutMilliseconds = settings.TimeoutMilliseconds.ToString();
        PingHost = settings.PingHost;
        HttpUrl = settings.HttpUrl;
        TcpHost = settings.TcpHost;
        TcpPort = settings.TcpPort.ToString();
        DataLimitMegabytes = settings.DataLimitMegabytes.ToString();
        NotifyOnFailure = settings.NotifyOnFailure;
        FailureThreshold = settings.FailureThreshold.ToString();
        PreventSleepWhileRunning = settings.PreventSleepWhileRunning;
        WeeklyStopEnabled = settings.WeeklyStopEnabled;
        WeeklyStopTime = settings.WeeklyStopTime.ToTimeSpan();
        foreach (var day in WeeklyStopDays)
            day.IsSelected = settings.WeeklyStopDays.HasFlag(day.Day.ToWeeklyDays());
        ExitAppOnAutoStop = settings.ExitAppOnAutoStop;
        AutoControlByNetwork = settings.AutoControlByNetwork;
        AutoControlNetworkName = settings.AutoControlNetworkName;
        StartOnLaunch = settings.StartOnLaunch;
        // LaunchAtLogin は OS 側の実際の状態を正とするため RefreshLaunchAtLoginAsync で別途取得する
    }

    /// <summary>ログイン時の自動起動が OS 上で有効になっているかを画面に反映する。</summary>
    private async Task RefreshLaunchAtLoginAsync()
    {
        try
        {
            LaunchAtLogin = _autoStart.IsSupported && await _autoStart.IsEnabledAsync();
        }
        catch (Exception ex)
        {
            _log.Warning(Text.Format(AppStrings.AutoStart_StateFailed, ex.Message));
        }
    }

    private async Task<bool> TryApplyAutoStartAsync(bool enabled)
    {
        try
        {
            if (!_autoStart.IsSupported || await _autoStart.IsEnabledAsync() == enabled)
                return true;

            await _autoStart.SetEnabledAsync(enabled);
            _log.Info(enabled ? AppStrings.AutoStart_Enabled : AppStrings.AutoStart_Disabled);
            return true;
        }
        catch (Exception ex)
        {
            ErrorText = Text.Format(AppStrings.AutoStart_SetFailed, ex.Message);
            _log.Error(ErrorText);
            return false;
        }
    }

    private static int ParseInt(string text, string label, List<string> errors)
    {
        if (int.TryParse(text?.Trim(), out var value))
            return value;

        errors.Add(Text.Format(AppStrings.Settings_IntegerRequired, label));
        return 0;
    }
}
