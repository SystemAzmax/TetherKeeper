namespace KeepAlive.Core.Models;

/// <summary>曜日の組み合わせ (毎週の自動停止の曜日)。</summary>
[Flags]
public enum WeeklyDays
{
    None = 0,
    Sunday = 1 << DayOfWeek.Sunday,
    Monday = 1 << DayOfWeek.Monday,
    Tuesday = 1 << DayOfWeek.Tuesday,
    Wednesday = 1 << DayOfWeek.Wednesday,
    Thursday = 1 << DayOfWeek.Thursday,
    Friday = 1 << DayOfWeek.Friday,
    Saturday = 1 << DayOfWeek.Saturday,

    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekends = Saturday | Sunday,
    Everyday = Weekdays | Weekends,
}

/// <summary>毎週決まった曜日・時刻の予定の計算。</summary>
public static class WeeklySchedule
{
    /// <summary>曜日の短い名前 (日曜から順に。表示言語に合わせる)。</summary>
    private static string[] DayNames => CoreStrings.Weekly_DayNames.Split(',');

    /// <summary>曜日を <see cref="WeeklyDays"/> の値にする。</summary>
    public static WeeklyDays ToWeeklyDays(this DayOfWeek day) => (WeeklyDays)(1 << (int)day);

    /// <summary>
    /// <paramref name="after"/> より後で、最初に来る指定の曜日・時刻 (<paramref name="zone"/> の時刻) を返す。曜日が無ければ null。
    /// </summary>
    public static DateTimeOffset? GetNextOccurrence(WeeklyDays days, TimeOnly time, DateTimeOffset after, TimeZoneInfo zone)
    {
        days &= WeeklyDays.Everyday;
        if (days == WeeklyDays.None)
            return null;

        var localDate = TimeZoneInfo.ConvertTime(after, zone).Date;
        // 今日から 1 週間後の同じ曜日まで (今日の時刻を過ぎていれば、来週の同じ曜日になる) を調べる
        for (var i = 0; i <= 7; i++)
        {
            var date = localDate.AddDays(i);
            if (!days.HasFlag(date.DayOfWeek.ToWeeklyDays()))
                continue;

            var local = DateTime.SpecifyKind(date + time.ToTimeSpan(), DateTimeKind.Unspecified);
            // 夏時間の切り替えで存在しない時刻は、1 時間後にずらす
            if (zone.IsInvalidTime(local))
                local = local.AddHours(1);

            var candidate = new DateTimeOffset(local, zone.GetUtcOffset(local));
            if (candidate > after)
                return candidate;
        }

        return null;
    }

    /// <summary>曜日を「毎日」「平日」「土日」「月・水・金」のように表す。</summary>
    public static string Format(WeeklyDays days)
    {
        days &= WeeklyDays.Everyday;
        return days switch
        {
            WeeklyDays.Everyday => CoreStrings.Weekly_Everyday,
            WeeklyDays.Weekdays => CoreStrings.Weekly_Weekdays,
            WeeklyDays.Weekends => CoreStrings.Weekly_Weekends,
            WeeklyDays.None => CoreStrings.Weekly_None,
            // 月曜始まりで並べる
            _ => string.Join(CoreStrings.Weekly_DaySeparator, Enumerable.Range(1, 7)
                .Select(i => (DayOfWeek)(i % 7))
                .Where(d => days.HasFlag(d.ToWeeklyDays()))
                .Select(GetDayName)),
        };
    }

    /// <summary>曜日の短い名前 (日、月、…)。</summary>
    public static string GetDayName(DayOfWeek day) => DayNames[(int)day];
}
