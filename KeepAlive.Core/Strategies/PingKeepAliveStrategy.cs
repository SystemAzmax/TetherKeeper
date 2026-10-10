using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Strategies;

/// <summary>
/// ICMP Echo による疎通。Windows では IcmpSendEcho2 (iphlpapi) が使われるため管理者権限は不要。
/// ホスト名の名前解決も含めてタイムアウト内に収める
/// (<see cref="Ping"/> にホスト名を渡すと、名前解決の時間はタイムアウトの対象外になるため自前で解決する)。
/// </summary>
public sealed class PingKeepAliveStrategy : IKeepAliveStrategy
{
    // Windows の ping.exe 既定と同じ 32 バイト
    private static readonly byte[] Payload = new byte[32];

    private readonly ISettingsService _settings;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost;

    public PingKeepAliveStrategy(ISettingsService settings, TimeProvider timeProvider)
        : this(settings, timeProvider, Dns.GetHostAddressesAsync)
    {
    }

    /// <param name="resolveHost">名前解決の処理 (テストで差し替える)。</param>
    internal PingKeepAliveStrategy(
        ISettingsService settings, TimeProvider timeProvider, Func<string, CancellationToken, Task<IPAddress[]>> resolveHost)
    {
        _settings = settings;
        _timeProvider = timeProvider;
        _resolveHost = resolveHost;
    }

    public string Name => "Ping";

    public KeepAliveMethod Method => KeepAliveMethod.Ping;

    public async Task<KeepAliveResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        var host = settings.PingHost;
        var timestamp = _timeProvider.GetLocalNow();
        var started = _timeProvider.GetTimestamp();

        using var timeoutCts = new CancellationTokenSource(settings.Timeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var address = await ResolveAsync(host, linkedCts.Token).ConfigureAwait(false);
            if (address is null)
                return KeepAliveResult.Failed(
                    Name, host, timestamp, _timeProvider.GetElapsedTime(started), "名前解決できませんでした。");

            // 名前解決にかかった分を差し引いた残り時間で Ping を送る
            var remaining = settings.Timeout - _timeProvider.GetElapsedTime(started);
            if (remaining < TimeSpan.FromMilliseconds(1))
                return TimedOut(settings, host, timestamp, started);

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(
                address, remaining, Payload, new PingOptions { DontFragment = true }, linkedCts.Token)
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
        catch (OperationCanceledException)
        {
            return TimedOut(settings, host, timestamp, started);
        }
        catch (SocketException ex)
        {
            // 名前解決の失敗 (存在しないホスト名など)
            return KeepAliveResult.Failed(Name, host, timestamp, _timeProvider.GetElapsedTime(started), ex.Message);
        }
        catch (ArgumentException ex)
        {
            // ホスト名として不正な文字列 (長すぎる等)
            return KeepAliveResult.Failed(Name, host, timestamp, _timeProvider.GetElapsedTime(started), ex.Message);
        }
        catch (PingException ex)
        {
            return KeepAliveResult.Failed(
                Name, host, timestamp, _timeProvider.GetElapsedTime(started), ex.InnerException?.Message ?? ex.Message);
        }
        catch (PlatformNotSupportedException)
        {
            return KeepAliveResult.Failed(
                Name, host, timestamp, _timeProvider.GetElapsedTime(started), "この OS では Ping を利用できません。");
        }
    }

    /// <summary>IP アドレスはそのまま、ホスト名は名前解決して最初のアドレスを返す (Ping クラスと同じ選び方)。</summary>
    private async Task<IPAddress?> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var address))
            return address;

        var addresses = await _resolveHost(host, cancellationToken).ConfigureAwait(false);
        return addresses.Length > 0 ? addresses[0] : null;
    }

    private KeepAliveResult TimedOut(AppSettings settings, string host, DateTimeOffset timestamp, long started) =>
        KeepAliveResult.Failed(
            Name, host, timestamp, _timeProvider.GetElapsedTime(started), $"タイムアウト ({settings.TimeoutMilliseconds} ms)");
}
