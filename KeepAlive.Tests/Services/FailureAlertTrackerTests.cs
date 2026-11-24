using KeepAlive.Core.Models;
using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Services;

public class FailureAlertTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly FailureAlertTracker _tracker = new();

    private static KeepAliveResult Failed(int minutes) =>
        KeepAliveResult.Failed("Ping", "1.1.1.1", Start.AddMinutes(minutes), TimeSpan.FromSeconds(3), "タイムアウト");

    private static KeepAliveResult Succeeded(int minutes) =>
        KeepAliveResult.Succeeded("Ping", "1.1.1.1", Start.AddMinutes(minutes), TimeSpan.FromMilliseconds(20));

    [Fact]
    public void Failing_IsReportedOnce_WhenThresholdIsReached()
    {
        Assert.Null(_tracker.Add(Failed(0), threshold: 3));
        Assert.Null(_tracker.Add(Failed(1), threshold: 3));
        Assert.False(_tracker.IsFailing);

        var alert = _tracker.Add(Failed(2), threshold: 3);

        Assert.NotNull(alert);
        Assert.Equal(FailureAlertKind.Failing, alert.Kind);
        Assert.Equal(3, alert.ConsecutiveFailures);
        Assert.Equal(Start, alert.FailingSince);
        Assert.True(_tracker.IsFailing);

        // 失敗が続いても、同じ状態では何度も知らせない
        Assert.Null(_tracker.Add(Failed(3), threshold: 3));
        Assert.True(_tracker.IsFailing);
    }

    [Fact]
    public void Recovered_IsReported_WithFailureCountAndDuration()
    {
        _tracker.Add(Failed(0), threshold: 2);
        _tracker.Add(Failed(1), threshold: 2);
        _tracker.Add(Failed(2), threshold: 2);

        var alert = _tracker.Add(Succeeded(5), threshold: 2);

        Assert.NotNull(alert);
        Assert.Equal(FailureAlertKind.Recovered, alert.Kind);
        Assert.Equal(3, alert.ConsecutiveFailures);
        Assert.Equal(TimeSpan.FromMinutes(5), alert.Duration);
        Assert.False(_tracker.IsFailing);
    }

    [Fact]
    public void Success_BeforeThreshold_IsNotReportedAsRecovery_AndResetsCount()
    {
        _tracker.Add(Failed(0), threshold: 3);
        _tracker.Add(Failed(1), threshold: 3);

        Assert.Null(_tracker.Add(Succeeded(2), threshold: 3));

        // 数え直しになるため、さらに 2 回失敗しても知らせない
        Assert.Null(_tracker.Add(Failed(3), threshold: 3));
        Assert.Null(_tracker.Add(Failed(4), threshold: 3));
        Assert.NotNull(_tracker.Add(Failed(5), threshold: 3));
    }

    [Fact]
    public void Reset_ForgetsFailures_SoNextSuccessIsNotReportedAsRecovery()
    {
        _tracker.Add(Failed(0), threshold: 1);
        Assert.True(_tracker.IsFailing);

        _tracker.Reset();

        Assert.False(_tracker.IsFailing);
        Assert.Null(_tracker.Add(Succeeded(1), threshold: 1));
    }

    [Fact]
    public void LoweredThreshold_IsAppliedToFailuresAlreadyCounted()
    {
        _tracker.Add(Failed(0), threshold: 5);
        _tracker.Add(Failed(1), threshold: 5);

        var alert = _tracker.Add(Failed(2), threshold: 2);

        Assert.Equal(FailureAlertKind.Failing, alert?.Kind);
    }

    [Fact]
    public void Failing_Message_ShowsCountAndLocalStartTime()
    {
        var alert = new FailureAlert(FailureAlertKind.Failing, 3, Start, null);

        Assert.Equal("通信の失敗が続いています", alert.Title);
        Assert.Contains("3 回続けて失敗しました", alert.Message);
        Assert.Contains($"{Start.ToLocalTime():HH:mm} から", alert.Message);
    }

    [Fact]
    public void Recovered_Message_ShowsCountAndDuration()
    {
        var alert = new FailureAlert(FailureAlertKind.Recovered, 4, Start, TimeSpan.FromMinutes(12));

        Assert.Equal("通信が復旧しました", alert.Title);
        Assert.Equal("4 回続けて失敗したあと、成功しました（失敗が続いた時間: 約 12 分）。", alert.Message);
    }

    [Theory]
    [InlineData(30, "1 分未満")]
    [InlineData(90, "約 2 分")]
    [InlineData(59 * 60, "約 59 分")]
    [InlineData(60 * 60, "約 1 時間")]
    [InlineData(80 * 60, "約 1 時間 20 分")]
    public void FormatDuration_RoundsToMinutes(int seconds, string expected)
    {
        Assert.Equal(expected, FailureAlert.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }
}
