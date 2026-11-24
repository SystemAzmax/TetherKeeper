namespace KeepAlive.Core.Models;

/// <summary>キープアライブが自動で停止した理由。</summary>
public enum AutoStopReason
{
    /// <summary>予約した日時 (<see cref="AppSettings.AutoStopAt"/>) になった。</summary>
    ScheduledTime,

    /// <summary>毎週の自動停止の時刻 (<see cref="AppSettings.WeeklyStopTime"/>) になった。</summary>
    WeeklySchedule,

    /// <summary>通信量の累計が上限 (<see cref="AppSettings.DataLimitMegabytes"/>) に達した。</summary>
    DataLimit,
}
