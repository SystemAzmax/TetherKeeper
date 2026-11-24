using KeepAlive.Core.Models;
using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Services;

public class TrafficEstimateTests
{
    [Fact]
    public void Ping_ThirtySeconds_IsAboutFifteenKilobytesPerHour()
    {
        // 1 時間に 120 回 × 120〜160 バイト
        var perHour = TrafficEstimate.PerHour(KeepAliveMethod.Ping, 30);

        Assert.Equal(new TrafficRange(14_400, 19_200), perHour);
        Assert.Equal("約 14〜19 KB", perHour!.Value.Format());
        // 24 時間では、設計判断.md の「約 0.35 MB」と同じくらい
        Assert.Equal("約 0.35〜0.46 MB", perHour.Value.Multiply(24).Format());
    }

    [Fact]
    public void LongerInterval_UsesLessData()
    {
        var thirty = TrafficEstimate.PerHour(KeepAliveMethod.TcpConnect, 30)!.Value;
        var sixty = TrafficEstimate.PerHour(KeepAliveMethod.TcpConnect, 60)!.Value;

        Assert.Equal(thirty.MinBytes / 2, sixty.MinBytes);
        Assert.Equal(thirty.MaxBytes / 2, sixty.MaxBytes);
    }

    [Fact]
    public void HttpHead_ReusesConnection_WhenIntervalIsShort()
    {
        // 30 秒間隔: 要求 120 回 + 接続の作り直し 30 回 (2 分ごと)
        var perHour = TrafficEstimate.PerHour(KeepAliveMethod.HttpHead, 30, "http://example.com/")!.Value;

        Assert.Equal(120 * 500 + 30 * 400, perHour.MinBytes);
        Assert.Equal(120 * 1_000 + 30 * 600, perHour.MaxBytes);
    }

    [Fact]
    public void HttpHead_ConnectsEveryTime_WhenIntervalIsLongerThanIdleTimeout()
    {
        // 120 秒間隔: 1 時間に 30 回、毎回接続する
        var perHour = TrafficEstimate.PerHour(KeepAliveMethod.HttpHead, 120, "http://example.com/")!.Value;

        Assert.Equal(30 * (500 + 400), perHour.MinBytes);
    }

    [Fact]
    public void Https_AddsTlsHandshake()
    {
        var http = TrafficEstimate.PerHour(KeepAliveMethod.HttpHead, 30, "http://example.com/")!.Value;
        var https = TrafficEstimate.PerHour(KeepAliveMethod.HttpHead, 30, "https://example.com/")!.Value;

        Assert.True(https.MinBytes > http.MinBytes);
        Assert.True(https.MaxBytes > http.MaxBytes);
    }

    [Fact]
    public void InvalidInterval_ReturnsNull()
    {
        Assert.Null(TrafficEstimate.PerHour(KeepAliveMethod.Ping, 0));
    }

    [Theory]
    [InlineData(500, 500, "約 0.50 KB")]
    [InlineData(5_000, 8_000, "約 5.0〜8.0 KB")]
    [InlineData(1_200_000, 2_500_000, "約 1.2〜2.5 MB")]
    [InlineData(800_000, 1_500_000, "約 0.80〜1.5 MB")]
    public void Format_UsesUnitOfLargerValue(long min, long max, string expected)
    {
        Assert.Equal(expected, new TrafficRange(min, max).Format());
    }
}
