using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace TetherKeeper.ViewModels;

public sealed record MethodOption(KeepAliveMethod Method, string DisplayName, string Description);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAutoStartService _autoStart;
    private readonly ILogService _log;

    public SettingsViewModel(ISettingsService settings, IAutoStartService autoStart, ILogService log)
    {
        _settings = settings;
        _autoStart = autoStart;
        _log = log;
        SelectedMethod = Methods[0];
        Load(_settings.Current);
        _ = RefreshLaunchAtLoginAsync();
    }

    public IReadOnlyList<MethodOption> Methods { get; } =
    [
        new(KeepAliveMethod.Ping, "Ping (ICMP)", "通信量が最小。管理者権限は不要です。ICMP を遮断するネットワークでは失敗します。"),
        new(KeepAliveMethod.HttpHead, "HTTP HEAD", "プロキシ環境や ICMP 遮断環境でも通りやすい方式です。通信量は Ping より多くなります。"),
        new(KeepAliveMethod.TcpConnect, "TCP Connect", "接続確立のみ行い即切断します。ポート 443 などへの接続が許可されている必要があります。"),
    ];

    public bool IsAutoStartSupported => _autoStart.IsSupported;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPing), nameof(IsHttpHead), nameof(IsTcpConnect), nameof(MethodDescription))]
    public partial MethodOption SelectedMethod { get; set; }

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

    public bool IsPing => SelectedMethod.Method == KeepAliveMethod.Ping;

    public bool IsHttpHead => SelectedMethod.Method == KeepAliveMethod.HttpHead;

    public bool IsTcpConnect => SelectedMethod.Method == KeepAliveMethod.TcpConnect;

    public string MethodDescription => SelectedMethod.Description;

    /// <summary>保存済みの設定を画面に再読み込みする (未保存の編集は破棄)。</summary>
    public async Task ReloadAsync()
    {
        Load(_settings.Current);
        ErrorText = string.Empty;
        StatusMessage = string.Empty;
        await RefreshLaunchAtLoginAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        var parseErrors = new List<string>();
        var interval = ParseInt(IntervalSeconds, "実行間隔", parseErrors);
        var timeout = ParseInt(TimeoutMilliseconds, "タイムアウト", parseErrors);
        var port = ParseInt(TcpPort, "TCP ポート", parseErrors);
        if (parseErrors.Count > 0)
        {
            ErrorText = string.Join(Environment.NewLine, parseErrors);
            return;
        }

        var updated = _settings.Current with
        {
            Method = SelectedMethod.Method,
            IntervalSeconds = interval,
            TimeoutMilliseconds = timeout,
            PingHost = PingHost.Trim(),
            HttpUrl = HttpUrl.Trim(),
            TcpHost = TcpHost.Trim(),
            TcpPort = port,
            StartOnLaunch = StartOnLaunch,
            LaunchAtLogin = LaunchAtLogin,
        };

        var errors = await _settings.SaveAsync(updated);
        if (errors.Count > 0)
        {
            ErrorText = string.Join(Environment.NewLine, errors);
            return;
        }

        ErrorText = string.Empty;
        if (!await TryApplyAutoStartAsync(LaunchAtLogin))
            return;

        StatusMessage = $"保存しました（{DateTime.Now:HH:mm:ss}）";
        Load(_settings.Current);
        await RefreshLaunchAtLoginAsync();
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        Load(defaults);
        StatusMessage = "既定値を入力しました。保存すると反映されます。";
    }

    private void Load(AppSettings settings)
    {
        SelectedMethod = Methods.FirstOrDefault(m => m.Method == settings.Method) ?? Methods[0];
        IntervalSeconds = settings.IntervalSeconds.ToString();
        TimeoutMilliseconds = settings.TimeoutMilliseconds.ToString();
        PingHost = settings.PingHost;
        HttpUrl = settings.HttpUrl;
        TcpHost = settings.TcpHost;
        TcpPort = settings.TcpPort.ToString();
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
            _log.Warning($"自動起動の状態を取得できませんでした: {ex.Message}");
        }
    }

    private async Task<bool> TryApplyAutoStartAsync(bool enabled)
    {
        try
        {
            if (!_autoStart.IsSupported || await _autoStart.IsEnabledAsync() == enabled)
                return true;

            await _autoStart.SetEnabledAsync(enabled);
            _log.Info(enabled ? "ログイン時の自動起動を有効にしました。" : "ログイン時の自動起動を無効にしました。");
            return true;
        }
        catch (Exception ex)
        {
            ErrorText = $"自動起動の設定に失敗しました: {ex.Message}";
            _log.Error(ErrorText);
            return false;
        }
    }

    private static int ParseInt(string text, string label, List<string> errors)
    {
        if (int.TryParse(text?.Trim(), out var value))
            return value;

        errors.Add($"{label}には整数を入力してください。");
        return 0;
    }
}
