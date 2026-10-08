using System.Net.Http.Headers;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Strategies;

/// <summary>
/// HTTP HEAD による疎通。HTTP 応答が返れば (ステータスコードに関わらず) 回線は生きているとみなす。
/// </summary>
public sealed class HttpHeadKeepAliveStrategy : IKeepAliveStrategy
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settings;
    private readonly TimeProvider _timeProvider;

    /// <param name="httpClient">
    /// タイムアウトは本クラスで制御するため <see cref="HttpClient.Timeout"/> は無限にしておくこと。
    /// </param>
    public HttpHeadKeepAliveStrategy(HttpClient httpClient, ISettingsService settings, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public string Name => "HTTP HEAD";

    public KeepAliveMethod Method => KeepAliveMethod.HttpHead;

    public async Task<KeepAliveResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        var url = settings.HttpUrl;
        var timestamp = _timeProvider.GetLocalNow();
        var started = _timeProvider.GetTimestamp();

        using var timeoutCts = new CancellationTokenSource(settings.Timeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            // 途中のキャッシュで応答されると実回線に通信が流れないため
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
                .ConfigureAwait(false);

            return KeepAliveResult.Succeeded(
                Name, url, timestamp, _timeProvider.GetElapsedTime(started),
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return KeepAliveResult.Failed(
                Name, url, timestamp, _timeProvider.GetElapsedTime(started), $"タイムアウト ({settings.TimeoutMilliseconds} ms)");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UriFormatException)
        {
            return KeepAliveResult.Failed(
                Name, url, timestamp, _timeProvider.GetElapsedTime(started), ex.InnerException?.Message ?? ex.Message);
        }
    }
}
