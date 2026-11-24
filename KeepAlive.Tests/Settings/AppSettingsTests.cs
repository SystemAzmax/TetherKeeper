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
            DataLimitMegabytes = 500,
        };

        Assert.Equal(settings, settings.Normalize());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(AppSettings.MaxDataLimitMegabytes + 1)]
    public void Validate_RejectsDataLimitOutOfRange(int megabytes)
    {
        var settings = new AppSettings { DataLimitMegabytes = megabytes };

        Assert.Contains(settings.Validate(), e => e.Contains("通信量の上限"));
    }

    [Fact]
    public void Normalize_ClampsDataLimit()
    {
        Assert.Equal(0, new AppSettings { DataLimitMegabytes = -5 }.Normalize().DataLimitMegabytes);
        Assert.Equal(
            AppSettings.MaxDataLimitMegabytes,
            new AppSettings { DataLimitMegabytes = int.MaxValue }.Normalize().DataLimitMegabytes);
    }

    [Fact]
    public void FailureNotification_IsOnWithThresholdOfThree_ByDefault()
    {
        var settings = new AppSettings();

        Assert.True(settings.NotifyOnFailure);
        Assert.Equal(3, settings.FailureThreshold);
    }

    [Fact]
    public void Validate_RequiresNetworkName_WhenAutoControlByNetworkIsOn()
    {
        Assert.Contains(
            new AppSettings { AutoControlByNetwork = true, AutoControlNetworkName = " " }.Validate(),
            e => e.Contains("ネットワークを選んで"));
        Assert.Empty(new AppSettings { AutoControlByNetwork = true, AutoControlNetworkName = "iPhone" }.Validate());
        Assert.Empty(new AppSettings { AutoControlByNetwork = false, AutoControlNetworkName = "" }.Validate());
    }

    [Fact]
    public void Normalize_TrimsNetworkName_AndDisablesAutoControlWithoutName()
    {
        var trimmed = new AppSettings { AutoControlByNetwork = true, AutoControlNetworkName = " iPhone " }.Normalize();
        Assert.Equal("iPhone", trimmed.AutoControlNetworkName);
        Assert.True(trimmed.AutoControlByNetwork);

        var empty = new AppSettings { AutoControlByNetwork = true, AutoControlNetworkName = "" }.Normalize();
        Assert.False(empty.AutoControlByNetwork);
        Assert.Empty(empty.Validate());
    }

    [Fact]
    public void Validate_RejectsFallbackSameAsMethod()
    {
        var settings = new AppSettings { Method = KeepAliveMethod.Ping, FallbackMethod = KeepAliveMethod.Ping };

        Assert.Contains(settings.Validate(), e => e.Contains("別の方式"));
    }

    [Fact]
    public void Validate_ChecksDestinationOfFallbackMethod()
    {
        var settings = new AppSettings { Method = KeepAliveMethod.Ping, FallbackMethod = KeepAliveMethod.HttpHead, HttpUrl = "" };

        Assert.Contains(settings.Validate(), e => e.Contains("URL"));
        Assert.Empty((settings with { HttpUrl = "http://example.com/" }).Validate());
    }

    [Fact]
    public void Normalize_ClearsInvalidFallback()
    {
        Assert.Null(new AppSettings { Method = KeepAliveMethod.Ping, FallbackMethod = KeepAliveMethod.Ping }.Normalize().FallbackMethod);
        Assert.Null(new AppSettings { FallbackMethod = (KeepAliveMethod)99 }.Normalize().FallbackMethod);
        Assert.Equal(
            KeepAliveMethod.HttpHead,
            new AppSettings { Method = KeepAliveMethod.Ping, FallbackMethod = KeepAliveMethod.HttpHead }.Normalize().FallbackMethod);
        Assert.Null(new AppSettings().FallbackMethod);
    }

    [Fact]
    public void Validate_RequiresWeeklyStopDays_WhenWeeklyStopIsOn()
    {
        Assert.Contains(
            new AppSettings { WeeklyStopEnabled = true, WeeklyStopDays = WeeklyDays.None }.Validate(),
            e => e.Contains("曜日"));
        Assert.Empty(new AppSettings { WeeklyStopEnabled = true, WeeklyStopDays = WeeklyDays.Weekdays }.Validate());
    }

    [Fact]
    public void Normalize_DropsSecondsFromWeeklyStopTime_AndDisablesWithoutDays()
    {
        var settings = new AppSettings
        {
            WeeklyStopEnabled = true,
            WeeklyStopTime = new TimeOnly(18, 30, 45),
            WeeklyStopDays = WeeklyDays.None,
        }.Normalize();

        Assert.Equal(new TimeOnly(18, 30), settings.WeeklyStopTime);
        Assert.False(settings.WeeklyStopEnabled);
    }

    [Fact]
    public void AutoStopExtensions_AreOffByDefault()
    {
        var settings = new AppSettings();

        Assert.False(settings.WeeklyStopEnabled);
        Assert.False(settings.ExitAppOnAutoStop);
        Assert.Equal(WeeklyDays.Everyday, settings.WeeklyStopDays);
    }

    [Fact]
    public void PreventSleepWhileRunning_IsOffByDefault()
    {
        // 電源の設定を尊重するため、既定ではスリープを妨げない
        Assert.False(new AppSettings().PreventSleepWhileRunning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(AppSettings.MaxFailureThreshold + 1)]
    public void Validate_RejectsFailureThresholdOutOfRange(int threshold)
    {
        var settings = new AppSettings { FailureThreshold = threshold };

        Assert.Contains(settings.Validate(), e => e.Contains("連続失敗の回数"));
    }

    [Fact]
    public void Normalize_ClampsFailureThreshold()
    {
        Assert.Equal(AppSettings.MinFailureThreshold, new AppSettings { FailureThreshold = 0 }.Normalize().FailureThreshold);
        Assert.Equal(
            AppSettings.MaxFailureThreshold,
            new AppSettings { FailureThreshold = int.MaxValue }.Normalize().FailureThreshold);
    }

    [Fact]
    public void DataLimitBytes_IsNullWhenDisabled_AndUsesDecimalMegabytes()
    {
        Assert.Null(new AppSettings().DataLimitBytes);
        Assert.Equal(500_000_000L, new AppSettings { DataLimitMegabytes = 500 }.DataLimitBytes);
    }
}
