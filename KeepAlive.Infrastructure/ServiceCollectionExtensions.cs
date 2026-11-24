using System.Runtime.Versioning;
using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Resources;
using KeepAlive.Core.Services;
using KeepAlive.Infrastructure.AutoStart;
using KeepAlive.Infrastructure.History;
using KeepAlive.Infrastructure.Logging;
using KeepAlive.Infrastructure.Network;
using KeepAlive.Infrastructure.Power;
using KeepAlive.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAlive.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 実行中の OS に応じた実装を登録する。
    /// </summary>
    /// <param name="settingsFilePath">設定ファイルのフルパス。</param>
    /// <param name="dataUsageFilePath">通信量の累計を保存するファイルのフルパス。</param>
    /// <param name="logDirectory">ログファイルを置くフォルダ。</param>
    /// <param name="historyDirectory">通信の結果の履歴 (CSV) を置くフォルダ。</param>
    /// <param name="appName">自動起動の登録名・ログファイル名などに使うアプリ名。</param>
    /// <param name="autoStartArguments">ログイン時の自動起動で渡す起動引数。</param>
    public static IServiceCollection AddKeepAliveInfrastructure(
        this IServiceCollection services,
        string settingsFilePath,
        string dataUsageFilePath,
        string logDirectory,
        string historyDirectory,
        string appName,
        string? autoStartArguments = null)
    {
        services.AddSingleton<ISettingsStore>(new JsonFileSettingsStore(settingsFilePath));
        services.AddSingleton<IDataUsageStore>(new JsonFileDataUsageStore(dataUsageFilePath));
        services.AddSingleton<IDataUsageMeter, NetworkInterfaceDataUsageMeter>();
        // 接続中のネットワーク名 (Wi-Fi の SSID など) は、.NET の標準の API では取れない。
        // Windows では UI 側で WinRT の API を使う実装に差し替える
        services.AddSingleton<INetworkMonitor, UnsupportedNetworkMonitor>();
        services.AddSingleton<ILogSink>(sp => new FileLogSink(
            logDirectory,
            appName,
            sp.GetRequiredService<KeepAliveOptions>().LogRetentionDays,
            sp.GetRequiredService<TimeProvider>()));
        // 履歴の保存日数はログと同じにする
        services.AddSingleton<IResultHistoryStore>(sp => new CsvResultHistoryStore(
            historyDirectory,
            appName,
            sp.GetRequiredService<KeepAliveOptions>().LogRetentionDays,
            sp.GetRequiredService<TimeProvider>()));

        if (OperatingSystem.IsWindows())
        {
            AddWindowsServices(services, appName, autoStartArguments);
        }
        else
        {
            // TODO: macOS は SMAppService (Login Items) で実装する
            services.AddSingleton<IPowerEventSource, NullPowerEventSource>();
            services.AddSingleton<ISleepInhibitor, UnsupportedSleepInhibitor>();
            services.AddSingleton<IAutoStartService, UnsupportedAutoStartService>();
        }

        return services;
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsServices(IServiceCollection services, string appName, string? autoStartArguments)
    {
        services.AddSingleton<IPowerEventSource, WindowsPowerEventSource>();
        // 理由は powercfg /requests に表示される。表示言語に合わせて日本語と英語を切り替える
        services.AddSingleton<ISleepInhibitor>(_ => new WindowsSleepInhibitor(
            Text.Format(CoreStrings.Sleep_Reason, AppDisplayName.GetCurrent())));
        services.AddSingleton<IAutoStartService>(Environment.ProcessPath is { } path
            ? new WindowsAutoStartService(appName, path, autoStartArguments)
            : new UnsupportedAutoStartService());
    }
}
