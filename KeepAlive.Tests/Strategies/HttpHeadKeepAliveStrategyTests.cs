using System.Net;
using KeepAlive.Core.Models;
using KeepAlive.Core.Strategies;

namespace KeepAlive.Tests.Strategies;

public class HttpHeadKeepAliveStrategyTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return send(request, cancellationToken);
        }
    }

    private static readonly AppSettings Settings = new()
    {
        Method = KeepAliveMethod.HttpHead,
        HttpUrl = "http://example.test/ping",
        TimeoutMilliseconds = 500,
    };

    private static HttpHeadKeepAliveStrategy CreateStrategy(StubHandler handler) =>
        new(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, new StaticSettingsService(Settings), TimeProvider.System);

    [Fact]
    public async Task ExecuteAsync_SendsHeadRequestWithNoCache()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await CreateStrategy(handler).ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Head, handler.LastRequest?.Method);
        Assert.Equal(new Uri(Settings.HttpUrl), handler.LastRequest?.RequestUri);
        Assert.True(handler.LastRequest?.Headers.CacheControl?.NoCache);
        Assert.Equal(Settings.HttpUrl, result.Target);
        Assert.Contains("200", result.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ExecuteAsync_TreatsAnyHttpResponseAsSuccess(HttpStatusCode status)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        var result = await CreateStrategy(handler).ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(((int)status).ToString(), result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotIncludeServerReasonPhrase()
    {
        // 平文 HTTP では経路上で書き換えられるため、サーバーの文言はログに残さない
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { ReasonPhrase = "INJECTED by server" }));

        var result = await CreateStrategy(handler).ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain("INJECTED", result.Message);
        Assert.Equal("HTTP 200 OK", result.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task ExecuteAsync_TreatsRedirectAsSuccessWithNote(HttpStatusCode status)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        var result = await CreateStrategy(handler).ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("リダイレクト", result.Message);
    }

    [Fact]
    public void DescribeStatus_ShowsOnlyCode_ForUndefinedStatus()
    {
        Assert.Equal("HTTP 599", HttpHeadKeepAliveStrategy.DescribeStatus((HttpStatusCode)599));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_OnNetworkError()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("network unreachable"));

        var result = await CreateStrategy(handler).ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("network unreachable", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTimeoutFailure_WhenServerDoesNotRespond()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await CreateStrategy(handler).ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("タイムアウト", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenCallerCancels()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateStrategy(handler).ExecuteAsync(cts.Token));
    }
}
