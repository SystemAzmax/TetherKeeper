using System.Net.Sockets;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Strategies;

/// <summary>
/// TCP 3 ウェイハンドシェイクの成立のみで疎通を確認し、すぐに切断する。データは送らない。
/// </summary>
public sealed class TcpConnectKeepAliveStrategy : IKeepAliveStrategy
{
    private readonly ISettingsService _settings;
    private readonly TimeProvider _timeProvider;

    public TcpConnectKeepAliveStrategy(ISettingsService settings, TimeProvider timeProvider)
    {
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public string Name => "TCP Connect";

    public KeepAliveMethod Method => KeepAliveMethod.TcpConnect;

    public async Task<KeepAliveResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        var host = settings.TcpHost;
        var port = settings.TcpPort;
        var target = $"{host}:{port}";
        var timestamp = _timeProvider.GetLocalNow();
        var started = _timeProvider.GetTimestamp();

        using var timeoutCts = new CancellationTokenSource(settings.Timeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // デュアルモードソケット (IPv4/IPv6 両対応)
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);
            var elapsed = _timeProvider.GetElapsedTime(started);
            var remote = socket.RemoteEndPoint?.ToString();

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
                // 切断時のエラーは疎通結果に影響しない
            }

            return KeepAliveResult.Succeeded(Name, target, timestamp, elapsed, $"接続成功 ({remote})");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return KeepAliveResult.Failed(
                Name, target, timestamp, _timeProvider.GetElapsedTime(started), $"タイムアウト ({settings.TimeoutMilliseconds} ms)");
        }
        catch (SocketException ex)
        {
            return KeepAliveResult.Failed(
                Name, target, timestamp, _timeProvider.GetElapsedTime(started), $"{ex.SocketErrorCode}: {ex.Message}");
        }
    }
}
