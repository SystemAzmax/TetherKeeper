using System.Net.NetworkInformation;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Strategies;

/// <summary>
/// ICMP Echo による疎通。Windows では IcmpSendEcho2 (iphlpapi) が使われるため管理者権限は不要。
/// </summary>
public sealed class PingKeepAliveStrategy : IKeepAliveStrategy
{
    // Windows の ping.exe 既定と同じ 32 バイト
    private static readonly byte[] Payload = new byte[32];

    private readonly ISettingsService _settings;
    private readonly TimeProvider _timeProvider;

    public PingKeepAliveStrategy(ISettingsService settings, TimeProvider timeProvider)
    {
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public string Name => "Ping";

    public KeepAliveMethod Method => KeepAliveMethod.Ping;

    public async Task<KeepAliveResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        var host = settings.PingHost;
        var timestamp = _timeProvider.GetLocalNow();
        var started = _timeProvider.GetTimestamp();

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(
                host, settings.Timeout, Payload, new PingOptions { DontFragment = true }, cancellationToken)
                .ConfigureAwait(false);
            var elapsed = _timeProvider.GetElapsedTime(started);

            return reply.Status == IPStatus.Success
                ? KeepAliveResult.Succeeded(Name, host, timestamp, elapsed, $"応答 {reply.RoundtripTime} ms ({reply.Address})")
                : KeepAliveResult.Failed(Name, host, timestamp, elapsed, $"応答なし: {reply.Status}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PingException ex)
        {
            // 名前解決失敗などは InnerException (SocketException) に詳細が入る
            return KeepAliveResult.Failed(
                Name, host, timestamp, _timeProvider.GetElapsedTime(started), ex.InnerException?.Message ?? ex.Message);
        }
        catch (PlatformNotSupportedException)
        {
            return KeepAliveResult.Failed(
                Name, host, timestamp, _timeProvider.GetElapsedTime(started), "この OS では Ping を利用できません。");
        }
    }
}
