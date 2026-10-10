using System.Net;
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
                Name, url, timestamp, _timeProvider.GetElapsedTime(started), DescribeStatus(response.StatusCode));
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

    /// <summary>
    /// 結果メッセージ用のステータス表記。
    /// サーバーが返す ReasonPhrase は使わない (平文 HTTP では経路上の第三者も書き換えられ、
    /// 任意の文字列がログに残ってしまうため)。
    /// </summary>
    internal static string DescribeStatus(HttpStatusCode status)
    {
        var code = (int)status;
        var text = Enum.IsDefined(status) ? $"HTTP {code} {status}" : $"HTTP {code}";

        // 自動リダイレクトは無効にしているため、3xx はそのまま返ってくる。
        // 回線は生きているので成功扱いだが、目的のサーバーではなくログイン画面 (キャプティブポータル) や
        // プロキシが応答している可能性がある
        return code is >= 300 and < 400
            ? $"{text}（リダイレクト応答: ログイン画面やプロキシが応答している可能性があります）"
            : text;
    }
}
