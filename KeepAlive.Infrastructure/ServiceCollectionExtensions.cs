using System.Runtime.Versioning;
using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Infrastructure.AutoStart;
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
    /// <param name="appName">自動起動の登録名・ログファイル名などに使うアプリ名。</param>
    /// <param name="autoStartArguments">ログイン時の自動起動で渡す起動引数。</param>
    public static IServiceCollection AddKeepAliveInfrastructure(
        this IServiceCollection services,
        string settingsFilePath,
        string dataUsageFilePath,
        string logDirectory,
        string appName,
        string? autoStartArguments = null)
    {
        services.AddSingleton<ISettingsStore>(new JsonFileSettingsStore(settingsFilePath));
        services.AddSingleton<IDataUsageStore>(new JsonFileDataUsageStore(dataUsageFilePath));
        services.AddSingleton<IDataUsageMeter, NetworkInterfaceDataUsageMeter>();
        services.AddSingleton<ILogSink>(sp => new FileLogSink(
            logDirectory,
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
            services.AddSingleton<IAutoStartService, UnsupportedAutoStartService>();
        }

        return services;
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsServices(IServiceCollection services, string appName, string? autoStartArguments)
    {
        services.AddSingleton<IPowerEventSource, WindowsPowerEventSource>();
        services.AddSingleton<IAutoStartService>(Environment.ProcessPath is { } path
            ? new WindowsAutoStartService(appName, path, autoStartArguments)
            : new UnsupportedAutoStartService());
    }
}
