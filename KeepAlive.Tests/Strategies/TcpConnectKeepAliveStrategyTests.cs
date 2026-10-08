using System.Net;
using System.Net.Sockets;
using KeepAlive.Core.Models;
using KeepAlive.Core.Strategies;

namespace KeepAlive.Tests.Strategies;

public class TcpConnectKeepAliveStrategyTests
{
    private static TcpConnectKeepAliveStrategy CreateStrategy(string host, int port, int timeoutMs = 3000) =>
        new(new StaticSettingsService(new AppSettings
        {
            Method = KeepAliveMethod.TcpConnect,
            TcpHost = host,
            TcpPort = port,
            TimeoutMilliseconds = timeoutMs,
        }), TimeProvider.System);

    [Fact]
    public async Task ExecuteAsync_Succeeds_WhenPortIsListening()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = await CreateStrategy("127.0.0.1", port).ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal($"127.0.0.1:{port}", result.Target);
        Assert.Equal("TCP Connect", result.StrategyName);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenPortIsClosed()
    {
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        var result = await CreateStrategy("127.0.0.1", port).ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenHostCannotBeResolved()
    {
        var result = await CreateStrategy("nonexistent.invalid", 443).ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
    }
}
