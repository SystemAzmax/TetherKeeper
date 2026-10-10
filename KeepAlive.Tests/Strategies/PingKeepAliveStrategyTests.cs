using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using KeepAlive.Core.Models;
using KeepAlive.Core.Strategies;

namespace KeepAlive.Tests.Strategies;

public class PingKeepAliveStrategyTests
{
    private static PingKeepAliveStrategy CreateStrategy(string host) =>
        new(new StaticSettingsService(new AppSettings { PingHost = host, TimeoutMilliseconds = 2000 }), TimeProvider.System);

    private static PingKeepAliveStrategy CreateStrategy(
        string host, Func<string, CancellationToken, Task<IPAddress[]>> resolveHost, int timeoutMilliseconds = 2000) =>
        new(new StaticSettingsService(new AppSettings { PingHost = host, TimeoutMilliseconds = timeoutMilliseconds }),
            TimeProvider.System, resolveHost);

    [Fact]
    public async Task ExecuteAsync_Succeeds_ForLoopback()
    {
        // Windows では管理者権限なしで成功することの確認も兼ねる
        var result = await CreateStrategy("127.0.0.1").ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Ping", result.StrategyName);
        Assert.Equal("127.0.0.1", result.Target);
    }

    [Fact]
    public async Task ExecuteAsync_TimesOut_WhenNameResolutionHangs()
    {
        var strategy = CreateStrategy("slow.test", async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }, timeoutMilliseconds: 500);
        var stopwatch = Stopwatch.StartNew();

        var result = await strategy.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("タイムアウト", result.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"タイムアウトが効いていません: {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotResolve_WhenHostIsIpAddress()
    {
        var strategy = CreateStrategy("127.0.0.1", (_, _) => throw new InvalidOperationException("名前解決は呼ばれないはず"));

        var result = await strategy.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PingsResolvedAddress()
    {
        var strategy = CreateStrategy("loopback.test", (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

        var result = await strategy.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("loopback.test", result.Target);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenResolverReturnsNoAddress()
    {
        var strategy = CreateStrategy("empty.test", (_, _) => Task.FromResult(Array.Empty<IPAddress>()));

        var result = await strategy.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenResolverThrowsSocketException()
    {
        var strategy = CreateStrategy("missing.test",
            (_, _) => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)));

        var result = await strategy.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenHostCannotBeResolved()
    {
        var result = await CreateStrategy("nonexistent.invalid").ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }
}
