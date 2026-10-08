using KeepAlive.Core.Models;
using KeepAlive.Core.Strategies;

namespace KeepAlive.Tests.Strategies;

public class PingKeepAliveStrategyTests
{
    private static PingKeepAliveStrategy CreateStrategy(string host) =>
        new(new StaticSettingsService(new AppSettings { PingHost = host, TimeoutMilliseconds = 2000 }), TimeProvider.System);

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
    public async Task ExecuteAsync_ReturnsFailure_WhenHostCannotBeResolved()
    {
        var result = await CreateStrategy("nonexistent.invalid").ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }
}
