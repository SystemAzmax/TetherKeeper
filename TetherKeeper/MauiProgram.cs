using KeepAlive.Core.Configuration;
using KeepAlive.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using TetherKeeper.Services;
using TetherKeeper.ViewModels;
using TetherKeeper.Views;

namespace TetherKeeper
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureLifecycleEvents(events =>
                {
#if WINDOWS
                    // タスクトレイ常駐 (× ボタンでトレイに格納)
                    events.AddWindows(windows => windows.OnWindowCreated(window =>
                    {
                        var services = IPlatformApplication.Current!.Services;
                        WinUI.App.Tray ??= new WinUI.TrayController(
                            window,
                            services.GetRequiredService<KeepAlive.Core.Interfaces.IKeepAliveWorker>(),
                            services.GetRequiredService<AppExitService>());
                    }));
#endif
                });

            builder.Services
                .AddKeepAliveCore()
                .AddKeepAliveInfrastructure(
                    AppPaths.SettingsFile,
                    AppPaths.LogDirectory,
                    appName: "TetherKeeper",
                    autoStartArguments: "--tray");

#if WINDOWS
            // MSIX 版はレジストリの Run キーが使えないため、StartupTask による自動起動に差し替える
            // (後から登録したものが優先される)
            if (AppPackage.IsPackaged)
                builder.Services.AddSingleton<KeepAlive.Core.Interfaces.IAutoStartService, WinUI.StartupTaskAutoStartService>();
#endif

            builder.Services.AddSingleton<AppExitService>();
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<SettingsPage>();
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
