using System.Net;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Services;
using KeepAlive.Core.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KeepAlive.Core.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// OS 非依存のサービスを登録する。
    /// <see cref="ISettingsStore"/> / <see cref="IDataUsageStore"/> / <see cref="IDataUsageMeter"/> /
    /// <see cref="IAutoStartService"/> / <see cref="IPowerEventSource"/> は
    /// プラットフォーム側 (Infrastructure) で別途登録すること。
    /// </summary>
    public static IServiceCollection AddKeepAliveCore(
        this IServiceCollection services, Action<KeepAliveOptions>? configure = null)
    {
        var options = new KeepAliveOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDataUsageTracker, DataUsageTracker>();

        services.AddSingleton<IKeepAliveStrategy, PingKeepAliveStrategy>();
        services.AddSingleton<IKeepAliveStrategy>(sp => new HttpHeadKeepAliveStrategy(
            CreateHttpClient(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IKeepAliveStrategy, TcpConnectKeepAliveStrategy>();
        services.AddSingleton<IKeepAliveStrategyResolver, KeepAliveStrategyResolver>();

        services.AddSingleton<IKeepAliveWorker, KeepAliveWorker>();
        return services;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            // テザリング再接続後に古い接続を使い続けないよう短めに
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(90),
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
        };

        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
