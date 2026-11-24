using AppKit;
using CoreGraphics;
using Foundation;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;

namespace TetherKeeper.Mac;

/// <summary>
/// 設定のウィンドウ。最小の版では、言語・通信方式・送信先・間隔・通知・起動の設定を扱う
/// (自動停止、スリープの抑止、ネットワークに応じた開始・停止、通信量の上限は、まだ macOS 版に無い)。
/// 保存の流れは Windows 版の SettingsViewModel と同じ。
/// </summary>
public sealed class SettingsWindowController : NSObject
{
    private const float FieldWidth = 300;

    private static readonly (KeepAliveMethod Method, string Name)[] Methods =
    [
        (KeepAliveMethod.Ping, "Ping (ICMP)"),
        (KeepAliveMethod.HttpHead, "HTTP HEAD"),
        (KeepAliveMethod.TcpConnect, "TCP Connect"),
    ];

    private readonly ISettingsService _settings;
    private readonly IAutoStartService _autoStart;
    private readonly ILogService _log;

    private readonly (string Code, string Name)[] _languages;
    private readonly (KeepAliveMethod? Method, string Name)[] _fallbacks;

    private NSWindow? _window;
    private NSPopUpButton _language = null!;
    private NSPopUpButton _method = null!;
    private NSPopUpButton _fallback = null!;
    private NSTextField _pingHost = null!;
    private NSTextField _httpUrl = null!;
    private NSTextField _tcpHost = null!;
    private NSTextField _tcpPort = null!;
    private NSTextField _interval = null!;
    private NSTextField _timeout = null!;
    private NSTextField _failureThreshold = null!;
    private NSButton _notifyOnFailure = null!;
    private NSButton _startOnLaunch = null!;
    private NSButton _launchAtLogin = null!;
    private NSTextField _message = null!;

    public SettingsWindowController(ISettingsService settings, IAutoStartService autoStart, ILogService log)
    {
        _settings = settings;
        _autoStart = autoStart;
        _log = log;

        // 言語名は、今の表示言語が読めなくても選べるよう、その言語自身で書く (Windows 版と同じ)
        _languages = [(string.Empty, MacStrings.Language_System), .. AppLanguages.Supported.Select(l => (l.Code, l.NativeName))];
        _fallbacks = [(null, AppStrings.Fallback_None), .. Methods.Select(m => ((KeepAliveMethod?)m.Method, m.Name))];
    }

    public void Show()
    {
        var isNew = _window is null;
        _window ??= CreateWindow();

        Load(_settings.Current);
        ShowMessage(string.Empty, isError: false);
        _ = RefreshLaunchAtLoginAsync();

        if (isNew)
            _window.Center();
        AppActivation.BringToFront();
        _window.MakeKeyAndOrderFront(this);
    }

    private NSWindow CreateWindow()
    {
        _language = CreatePopUp(_languages.Select(l => l.Name));
        _method = CreatePopUp(Methods.Select(m => m.Name));
        _fallback = CreatePopUp(_fallbacks.Select(f => f.Name));
        _pingHost = CreateTextField(FieldWidth);
        _httpUrl = CreateTextField(FieldWidth);
        _tcpHost = CreateTextField(FieldWidth);
        _tcpPort = CreateTextField(80);
        _interval = CreateTextField(80);
        _timeout = CreateTextField(80);
        _failureThreshold = CreateTextField(80);
        _notifyOnFailure = CreateCheckbox(AppStrings.Settings_NotifyOnFailure);
        _startOnLaunch = CreateCheckbox(AppStrings.Settings_StartOnLaunch);
        _launchAtLogin = CreateCheckbox(MacStrings.Settings_LaunchAtLogin);
        _launchAtLogin.Enabled = _autoStart.IsSupported;

        var empty = NSGridCell.EmptyContentView;
        NSView[][] rows =
        [
            [Label(AppStrings.Settings_Language), _language],
            [Label(AppStrings.Settings_Method), _method],
            [Label(AppStrings.Settings_Fallback), _fallback],
            [Label(AppStrings.Settings_PingHost), _pingHost],
            [Label(AppStrings.Settings_HttpUrl), _httpUrl],
            [Label(AppStrings.Settings_TcpHost), _tcpHost],
            [Label(AppStrings.Settings_Port), _tcpPort],
            [Label(AppStrings.Settings_Interval), _interval],
            [Label(AppStrings.Settings_Timeout), _timeout],
            [Label(AppStrings.Settings_FailureThreshold), _failureThreshold],
            [empty, _notifyOnFailure],
            [empty, _startOnLaunch],
            [empty, _launchAtLogin],
        ];
        var grid = NSGridView.Create(rows);
        grid.RowSpacing = 8;
        grid.ColumnSpacing = 12;
        grid.RowAlignment = NSGridRowAlignment.FirstBaseline;
        grid.GetColumn(0).XPlacement = NSGridCellPlacement.Trailing;

        _message = NSTextField.CreateWrappingLabel(string.Empty);
        _message.PreferredMaxLayoutWidth = 520;

        var hint = NSTextField.CreateWrappingLabel(AppStrings.Settings_Disclaimer);
        hint.TextColor = NSColor.SystemGray;
        hint.Font = NSFont.SystemFontOfSize(NSFont.SmallSystemFontSize);
        hint.PreferredMaxLayoutWidth = 520;

        var restoreDefaults = CreateButton(AppStrings.Settings_RestoreDefaults, () =>
        {
            Load(new AppSettings());
            ShowMessage(AppStrings.Settings_DefaultsEntered, isError: false);
        });
        var close = CreateButton(MacStrings.Settings_Close, () => _window?.Close());
        close.KeyEquivalent = "\u001b"; // esc
        var save = CreateButton(AppStrings.Settings_Save, () => _ = SaveAsync());
        save.KeyEquivalent = "\r"; // return (既定のボタン)

        // 「既定値に戻す」を左に、「閉じる」「保存」を右に寄せる
        var spacer = new NSView();
        spacer.SetContentHuggingPriorityForOrientation(1, NSLayoutConstraintOrientation.Horizontal);
        var buttons = NSStackView.FromViews([restoreDefaults, spacer, close, save]);
        buttons.Orientation = NSUserInterfaceLayoutOrientation.Horizontal;

        var root = NSStackView.FromViews([grid, _message, hint, buttons]);
        root.Orientation = NSUserInterfaceLayoutOrientation.Vertical;
        root.Alignment = NSLayoutAttribute.Leading;
        root.Spacing = 16;
        root.EdgeInsets = new NSEdgeInsets(20, 20, 20, 20);
        buttons.WidthAnchor.ConstraintEqualTo(grid.WidthAnchor).Active = true;
        _message.WidthAnchor.ConstraintEqualTo(grid.WidthAnchor).Active = true;
        hint.WidthAnchor.ConstraintEqualTo(grid.WidthAnchor).Active = true;

        var window = new NSWindow(
            new CGRect(0, 0, 600, 560),
            NSWindowStyle.Titled | NSWindowStyle.Closable,
            NSBackingStore.Buffered,
            false)
        {
            Title = AppStrings.Tab_Settings,
            ReleasedWhenClosed = false,
            ContentView = root,
        };
        root.LayoutSubtreeIfNeeded();
        window.SetContentSize(root.FittingSize);
        return window;
    }

    private void Load(AppSettings settings)
    {
        _language.SelectItem(Math.Max(0, Array.FindIndex(_languages, l => l.Code == settings.Language)));
        _method.SelectItem(Math.Max(0, Array.FindIndex(Methods, m => m.Method == settings.Method)));
        _fallback.SelectItem(Math.Max(0, Array.FindIndex(_fallbacks, f => f.Method == settings.FallbackMethod)));
        _pingHost.StringValue = settings.PingHost;
        _httpUrl.StringValue = settings.HttpUrl;
        _tcpHost.StringValue = settings.TcpHost;
        _tcpPort.StringValue = settings.TcpPort.ToString();
        _interval.StringValue = settings.IntervalSeconds.ToString();
        _timeout.StringValue = settings.TimeoutMilliseconds.ToString();
        _failureThreshold.StringValue = settings.FailureThreshold.ToString();
        _notifyOnFailure.State = ToState(settings.NotifyOnFailure);
        _startOnLaunch.State = ToState(settings.StartOnLaunch);
        // ログイン時の起動は、macOS 側の実際の状態を正とするため RefreshLaunchAtLoginAsync で別に読む
    }

    private async Task SaveAsync()
    {
        var parseErrors = new List<string>();
        var interval = ParseInt(_interval.StringValue, AppStrings.Label_Interval, parseErrors);
        var timeout = ParseInt(_timeout.StringValue, AppStrings.Label_Timeout, parseErrors);
        var port = ParseInt(_tcpPort.StringValue, AppStrings.Label_TcpPort, parseErrors);
        var failureThreshold = ParseInt(_failureThreshold.StringValue, AppStrings.Label_FailureThreshold, parseErrors);
        if (parseErrors.Count > 0)
        {
            ShowMessage(string.Join(Environment.NewLine, parseErrors), isError: true);
            return;
        }

        // この画面で扱う項目だけを、保存直前の最新の設定に上書きする
        // (Windows 版の設定ファイルを持ってきた場合などに、この画面に無い項目を消さないため)
        var language = _languages[SelectedIndex(_language)].Code;
        var method = Methods[SelectedIndex(_method)].Method;
        var fallbackMethod = _fallbacks[SelectedIndex(_fallback)].Method;
        var pingHost = _pingHost.StringValue.Trim();
        var httpUrl = _httpUrl.StringValue.Trim();
        var tcpHost = _tcpHost.StringValue.Trim();
        var notifyOnFailure = _notifyOnFailure.State == NSCellStateValue.On;
        var startOnLaunch = _startOnLaunch.State == NSCellStateValue.On;
        var launchAtLogin = _launchAtLogin.State == NSCellStateValue.On;
        var previousLanguage = _settings.Current.Language;

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
            NotifyOnFailure = notifyOnFailure,
            FailureThreshold = failureThreshold,
            StartOnLaunch = startOnLaunch,
            LaunchAtLogin = launchAtLogin,
        });
        if (errors.Count > 0)
        {
            ShowMessage(string.Join(Environment.NewLine, errors), isError: true);
            return;
        }

        if (!await TryApplyAutoStartAsync(launchAtLogin))
            return;

        Load(_settings.Current);
        await RefreshLaunchAtLoginAsync();

        // 画面の文言は作るときに読み込むため、言語は次に起動したときに切り替わる
        ShowMessage(
            _settings.Current.Language != previousLanguage
                ? AppStrings.Language_AppliesNextStart
                : Text.Format(AppStrings.Settings_SavedAt, DateTime.Now),
            isError: false);
    }

    private async Task RefreshLaunchAtLoginAsync()
    {
        try
        {
            _launchAtLogin.State = ToState(_autoStart.IsSupported && await _autoStart.IsEnabledAsync());
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
            var message = Text.Format(AppStrings.AutoStart_SetFailed, ex.Message);
            _log.Error(message);
            ShowMessage(message, isError: true);
            await RefreshLaunchAtLoginAsync();
            return false;
        }
    }

    private void ShowMessage(string text, bool isError)
    {
        _message.StringValue = text;
        _message.TextColor = isError ? NSColor.SystemRed : NSColor.SystemGray;
        _message.Hidden = text.Length == 0;
    }

    private static int ParseInt(string text, string label, List<string> errors)
    {
        if (int.TryParse(text?.Trim(), out var value))
            return value;

        errors.Add(Text.Format(AppStrings.Settings_IntegerRequired, label));
        return 0;
    }

    private static int SelectedIndex(NSPopUpButton popUp) => Math.Max(0, (int)popUp.IndexOfSelectedItem);

    private static NSCellStateValue ToState(bool value) => value ? NSCellStateValue.On : NSCellStateValue.Off;

    private static NSTextField Label(string text) => NSTextField.CreateLabel(text);

    private static NSTextField CreateTextField(float width)
    {
        var field = NSTextField.CreateTextField(string.Empty);
        field.WidthAnchor.ConstraintEqualTo(width).Active = true;
        return field;
    }

    private static NSPopUpButton CreatePopUp(IEnumerable<string> items)
    {
        var popUp = new NSPopUpButton(CGRect.Empty, false);
        popUp.AddItems(items.ToArray());
        return popUp;
    }

    private static NSButton CreateCheckbox(string title)
    {
        var checkbox = new NSButton { Title = title };
        checkbox.SetButtonType(NSButtonType.Switch);
        return checkbox;
    }

    private static NSButton CreateButton(string title, Action action)
    {
        var button = new NSButton { Title = title, BezelStyle = NSBezelStyle.Rounded };
        button.SetButtonType(NSButtonType.MomentaryPushIn);
        button.Activated += (_, _) => action();
        return button;
    }
}
