using KeepAlive.Core.Models;

namespace KeepAlive.Tests.Services;

public class WeeklyScheduleTests
{
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone("Test+9", TimeSpan.FromHours(9), "Test+9", "Test+9");

    // 2026-11-24 は火曜日
    private static DateTimeOffset Local(int day, int hour, int minute = 0) =>
        new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(9));

    [Fact]
    public void GetNextOccurrence_ReturnsTodayWhenTimeIsStillAhead()
    {
        var next = WeeklySchedule.GetNextOccurrence(WeeklyDays.Everyday, new TimeOnly(18, 0), Local(24, 10), Tokyo);

        Assert.Equal(Local(24, 18), next);
    }

    [Fact]
    public void GetNextOccurrence_SkipsToNextMatchingDay_WhenTimeHasPassed()
    {
        // 火曜の 19 時の後で、平日の 18 時 → 水曜の 18 時
        var next = WeeklySchedule.GetNextOccurrence(WeeklyDays.Weekdays, new TimeOnly(18, 0), Local(24, 19), Tokyo);

        Assert.Equal(Local(25, 18), next);
    }

    [Fact]
    public void GetNextOccurrence_ExactTimeIsNotIncluded()
    {
        var next = WeeklySchedule.GetNextOccurrence(WeeklyDays.Tuesday, new TimeOnly(18, 0), Local(24, 18), Tokyo);

        // 同じ曜日・時刻ちょうどは含めず、翌週になる
        Assert.Equal(Local(24, 18).AddDays(7), next);
    }

    [Fact]
    public void GetNextOccurrence_SkipsWeekend()
    {
        // 金曜 (11/27) の 20 時の後で、平日の 9 時 → 月曜 (11/30) の 9 時
        var next = WeeklySchedule.GetNextOccurrence(WeeklyDays.Weekdays, new TimeOnly(9, 0), Local(27, 20), Tokyo);

        Assert.Equal(Local(30, 9), next);
    }

    [Fact]
    public void GetNextOccurrence_UsesTheGivenTimeZone()
    {
        // UTC で表した日時でも、指定したタイムゾーンの曜日・時刻で数える
        var next = WeeklySchedule.GetNextOccurrence(
            WeeklyDays.Everyday, new TimeOnly(18, 0), Local(24, 10).ToUniversalTime(), Tokyo);

        Assert.Equal(Local(24, 18), next);
    }

    [Fact]
    public void GetNextOccurrence_ReturnsNull_WhenNoDays()
    {
        Assert.Null(WeeklySchedule.GetNextOccurrence(WeeklyDays.None, new TimeOnly(18, 0), Local(24, 10), Tokyo));
    }

    [Theory]
    [InlineData(WeeklyDays.Everyday, "毎日")]
    [InlineData(WeeklyDays.Weekdays, "平日")]
    [InlineData(WeeklyDays.Weekends, "土日")]
    [InlineData(WeeklyDays.Monday | WeeklyDays.Wednesday | WeeklyDays.Friday, "月・水・金")]
    [InlineData(WeeklyDays.Sunday | WeeklyDays.Monday, "月・日")]
    public void Format_ShowsDaysStartingFromMonday(WeeklyDays days, string expected)
    {
        Assert.Equal(expected, WeeklySchedule.Format(days));
    }
}
