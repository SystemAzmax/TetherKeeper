using KeepAlive.Core.Models;

namespace KeepAlive.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        Assert.Empty(new AppSettings().Validate());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3601)]
    public void Validate_RejectsIntervalOutOfRange(int interval)
    {
        var settings = new AppSettings { IntervalSeconds = interval };

        Assert.Contains(settings.Validate(), e => e.Contains("実行間隔"));
    }

    [Theory]
    [InlineData("http://user:secret@example.com/ping")]
    [InlineData("https://user@example.com/")]
    public void Validate_RejectsHttpUrlWithUserInfo_WhenHttpHeadSelected(string url)
    {
        var settings = new AppSettings { Method = KeepAliveMethod.HttpHead, HttpUrl = url };

        Assert.Contains(settings.Validate(), e => e.Contains("パスワード"));
    }

    [Fact]
    public void Normalize_RemovesUserInfoFromHttpUrl()
    {
        var settings = new AppSettings { HttpUrl = "http://user:secret@example.com:8080/ping?a=1" }.Normalize();

        Assert.Equal("http://example.com:8080/ping?a=1", settings.HttpUrl);
    }

    [Fact]
    public void Normalize_KeepsHttpUrlAsIs_WhenNoUserInfo()
    {
        // 表記 (末尾スラッシュの有無など) を勝手に変えない
        var settings = new AppSettings { HttpUrl = "http://example.com" }.Normalize();

        Assert.Equal("http://example.com", settings.HttpUrl);
    }

    [Fact]
    public void Validate_RejectsTimeoutNotShorterThanInterval()
    {
        var settings = new AppSettings { IntervalSeconds = 5, TimeoutMilliseconds = 5000 };

        Assert.Contains(settings.Validate(), e => e.Contains("実行間隔より短く"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com/")]
    public void Validate_RejectsInvalidHttpUrl_WhenHttpHeadSelected(string url)
    {
        var settings = new AppSettings { Method = KeepAliveMethod.HttpHead, HttpUrl = url };

        Assert.Contains(settings.Validate(), e => e.Contains("URL"));
    }

    [Fact]
    public void Validate_IgnoresFieldsOfUnselectedMethod()
    {
        var settings = new AppSettings { Method = KeepAliveMethod.Ping, HttpUrl = "", TcpPort = 0 };

        Assert.Empty(settings.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_RejectsInvalidTcpPort(int port)
    {
        var settings = new AppSettings { Method = KeepAliveMethod.TcpConnect, TcpPort = port };

        Assert.Contains(settings.Validate(), e => e.Contains("ポート"));
    }

    [Fact]
    public void Normalize_ClampsAndRepairsInvalidValues()
    {
        var broken = new AppSettings
        {
            Method = (KeepAliveMethod)99,
            IntervalSeconds = 1,
            TimeoutMilliseconds = 100_000,
            PingHost = "  ",
            HttpUrl = "not a url",
            TcpHost = " example.com ",
            TcpPort = -1,
        };

        var normalized = broken.Normalize();
        var defaults = new AppSettings();

        Assert.Equal(defaults.Method, normalized.Method);
        Assert.Equal(AppSettings.MinIntervalSeconds, normalized.IntervalSeconds);
        Assert.True(normalized.TimeoutMilliseconds < normalized.IntervalSeconds * 1000);
        Assert.Equal(defaults.PingHost, normalized.PingHost);
        Assert.Equal(defaults.HttpUrl, normalized.HttpUrl);
        Assert.Equal("example.com", normalized.TcpHost);
        Assert.Equal(defaults.TcpPort, normalized.TcpPort);
        Assert.Empty(normalized.Validate());
    }

    [Fact]
    public void Normalize_KeepsValidSettingsUnchanged()
    {
        var settings = new AppSettings
        {
            Method = KeepAliveMethod.TcpConnect,
            IntervalSeconds = 20,
            TimeoutMilliseconds = 2000,
            TcpHost = "example.com",
            TcpPort = 80,
            StartOnLaunch = true,
        };

        Assert.Equal(settings, settings.Normalize());
    }
}
