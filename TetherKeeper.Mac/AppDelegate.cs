using AppKit;
using Foundation;
using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Services;
using KeepAlive.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ObjCRuntime;
using TetherKeeper.Mac.Services;

namespace TetherKeeper.Mac;

/// <summary>
/// アプリの起動と終了。サービスを組み立て、メニューバーのアイコンを出し、設定に従ってキープアライブを開始する。
/// </summary>
[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private ServiceProvider? _services;
    private StatusMenuController? _statusMenu;
    private SettingsWindowController? _settingsWindow;
    private bool _terminating;

    public override void DidFinishLaunching(NSNotification notification)
    {
        // 文言を使う前に、表示言語を決める
        ApplyLanguage();
        NSApplication.SharedApplication.MainMenu = CreateEditMenu();

        var services = new ServiceCollection();
        services
            .AddKeepAliveCore()
            .AddKeepAliveInfrastructure(
                MacAppPaths.SettingsFile,
                MacAppPaths.DataUsageFile,
                MacAppPaths.LogDirectory,
                MacAppPaths.HistoryDirectory,
                appName: "TetherKeeper");
        // Infrastructure の「未対応」の実装から差し替える (後から登録したものが使われる)
        services.AddSingleton<IAutoStartService, MacAutoStartService>();
        services.AddSingleton<MacNotifier>();
        _services = services.BuildServiceProvider();

        // 通信の結果の履歴は、起動したときから記録する (作った時点から Worker の結果を記録する)
        _services.GetRequiredService<ResultHistory>();

        _settingsWindow = new SettingsWindowController(
            _services.GetRequiredService<ISettingsService>(),
            _services.GetRequiredService<IAutoStartService>(),
            _services.GetRequiredService<ILogService>());
        _statusMenu = new StatusMenuController(
            _services.GetRequiredService<IKeepAliveWorker>(),
            _services.GetRequiredService<ISettingsService>(),
            _services.GetRequiredService<IKeepAliveStrategyResolver>(),
            _services.GetRequiredService<ILogService>(),
            _services.GetRequiredService<MacNotifier>(),
            () => _settingsWindow.Show());

        _ = InitializeAsync(_services);
    }

    /// <summary>設定を読み込み、設定に従ってキープアライブを開始する。</summary>
    private async Task InitializeAsync(IServiceProvider services)
    {
        var log = services.GetRequiredService<ILogService>();
        try
        {
            log.Info(Text.Format(
                AppStrings.App_Started,
                AppDisplayName.GetCurrent(),
                AppAboutInfo.FormatVersion(GetVersion()),
                IsFromAppStore() ? CoreStrings.About_StoreEdition : MacStrings.About_DevEdition));

            var settings = await services.GetRequiredService<ISettingsService>().LoadAsync();
            await services.GetRequiredService<IDataUsageTracker>().LoadAsync();
            _statusMenu?.Refresh();

            // 通知を使う設定なら、最初の失敗を待たずに許可を求めておく (失敗が続いてから聞かれても間に合わないため)
            if (settings.NotifyOnFailure)
                services.GetRequiredService<MacNotifier>().RequestAuthorization();

            if (settings.StartOnLaunch)
                services.GetRequiredService<IKeepAliveWorker>().Start();
        }
        catch (Exception ex)
        {
            log.Error(ex.Message);
        }
    }

    /// <summary>
    /// 終了する前に、キープアライブを止めてログを書き出す (Windows 版の AppExitService と同じ)。
    /// メニューの「終了」のほか、Mac のログアウト・再起動のときも呼ばれる。
    /// </summary>
    public override NSApplicationTerminateReply ApplicationShouldTerminate(NSApplication sender)
    {
        if (_services is null)
            return NSApplicationTerminateReply.Now;
        if (_terminating)
            return NSApplicationTerminateReply.Later;

        _terminating = true;
        _ = ShutdownAsync(sender, _services);
        return NSApplicationTerminateReply.Later;
    }

    private static async Task ShutdownAsync(NSApplication application, IServiceProvider services)
    {
        var log = services.GetRequiredService<ILogService>();
        try
        {
            // 停止やログの書き出しが終わらない場合も、終了は妨げない
            await Task.WhenAny(services.GetRequiredService<IKeepAliveWorker>().StopAsync(), Task.Delay(TimeSpan.FromSeconds(5)));
            log.Info(Text.Format(AppStrings.Exit_Exiting, AppDisplayName.GetCurrent()));
            await Task.WhenAny(log.FlushAsync(), Task.Delay(TimeSpan.FromSeconds(3)));
        }
        catch (Exception ex)
        {
            log.Error(Text.Format(AppStrings.Exit_Error, ex.Message));
        }
        finally
        {
            application.InvokeOnMainThread(() => application.ReplyToApplicationShouldTerminate(true));
        }
    }

    /// <summary>
    /// 設定で選んだ表示言語を反映する。設定が空 (OS の言語に合わせる) なら、macOS の言語の設定
    /// (システム設定の「言語と地域」の、アプリごとの言語を含む) から、このアプリが用意している言語で合うものを使う。
    /// </summary>
    private static void ApplyLanguage()
    {
        try
        {
            var saved = AppLanguageStartup.ReadSavedLanguage(MacAppPaths.SettingsFile);
            AppLanguageStartup.Apply(string.IsNullOrEmpty(saved)
                ? NSBundle.MainBundle.PreferredLocalizations.FirstOrDefault()
                : saved);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppDelegate: 表示言語を反映できませんでした: {ex}");
        }
    }

    /// <summary>
    /// 見えないメインメニュー。メニューバー常駐のアプリ (LSUIElement) はメニューが表示されないが、
    /// ⌘C・⌘V などのキー操作はメインメニューの項目で処理されるため、設定の入力欄で使えるよう編集の項目を置く。
    /// </summary>
    private static NSMenu CreateEditMenu()
    {
        var edit = new NSMenu("Edit");
        edit.AddItem(new NSMenuItem("Undo", new Selector("undo:"), "z"));
        edit.AddItem(new NSMenuItem("Redo", new Selector("redo:"), "Z"));
        edit.AddItem(NSMenuItem.SeparatorItem);
        edit.AddItem(new NSMenuItem("Cut", new Selector("cut:"), "x"));
        edit.AddItem(new NSMenuItem("Copy", new Selector("copy:"), "c"));
        edit.AddItem(new NSMenuItem("Paste", new Selector("paste:"), "v"));
        edit.AddItem(new NSMenuItem("Select All", new Selector("selectAll:"), "a"));
        edit.AddItem(NSMenuItem.SeparatorItem);
        edit.AddItem(new NSMenuItem("Close Window", new Selector("performClose:"), "w"));

        var editItem = new NSMenuItem { Title = "Edit", Submenu = edit };
        var main = new NSMenu();
        main.AddItem(new NSMenuItem { Title = "App", Submenu = new NSMenu("App") });
        main.AddItem(editItem);
        return main;
    }

    private static Version GetVersion() =>
        NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString")?.ToString() is { } text
            && Version.TryParse(text, out var version)
            ? version
            : new Version(0, 0, 0);

    /// <summary>Mac App Store から入れたアプリか (App Store のレシートがあるか)。</summary>
    private static bool IsFromAppStore() =>
        NSBundle.MainBundle.AppStoreReceiptUrl?.Path is { } path && File.Exists(path);
}
