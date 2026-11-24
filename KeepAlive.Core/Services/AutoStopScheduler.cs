using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>
/// 日時を指定した自動停止 (<see cref="AppSettings.AutoStopAt"/>) の予約と解除。
/// モニター画面とトレイのメニューの両方から使う。
/// </summary>
public sealed class AutoStopScheduler
{
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly TimeProvider _timeProvider;

    public AutoStopScheduler(ISettingsService settings, ILogService log, TimeProvider timeProvider)
    {
        _settings = settings;
        _log = log;
        _timeProvider = timeProvider;
    }

    /// <summary>予約している日時 (PC の時刻)。予約していないか、既に過ぎていれば null。</summary>
    public DateTimeOffset? ScheduledAt =>
        _settings.Current.AutoStopAt is { } at && at > _timeProvider.GetUtcNow()
            ? TimeZoneInfo.ConvertTime(at, _timeProvider.LocalTimeZone)
            : null;

    /// <summary>指定した日時に予約する。エラーがあれば、その内容を返す。</summary>
    public Task<IReadOnlyList<string>> ScheduleAtAsync(DateTimeOffset stopAt)
    {
        if (stopAt <= _timeProvider.GetUtcNow())
            return Task.FromResult<IReadOnlyList<string>>(["現在より後の日時を指定してください。"]);

        return SaveAsync(stopAt);
    }

    /// <summary>
    /// 今から指定した時間の後に予約する。予約済みなら置き換える。
    /// 表示は分単位のため、今の時刻の秒を切り上げて、指定した時間より前に止まらないようにする。
    /// </summary>
    public Task<IReadOnlyList<string>> ScheduleAfterAsync(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        return SaveAsync(RoundUpToMinute(_timeProvider.GetUtcNow()) + duration);
    }

    /// <summary>予約を解除する。</summary>
    public Task<IReadOnlyList<string>> CancelAsync() => SaveAsync(null);

    /// <summary>秒以下を切り上げて、次の分ちょうどにする (ちょうどならそのまま)。</summary>
    public static DateTimeOffset RoundUpToMinute(DateTimeOffset value)
    {
        var remainder = value.Ticks % TimeSpan.TicksPerMinute;
        return remainder == 0 ? value : value.AddTicks(TimeSpan.TicksPerMinute - remainder);
    }

    private async Task<IReadOnlyList<string>> SaveAsync(DateTimeOffset? stopAt)
    {
        // 保存する日時は PC の時刻 (タイムゾーン) で表す
        var local = stopAt is { } at ? TimeZoneInfo.ConvertTime(at, _timeProvider.LocalTimeZone) : (DateTimeOffset?)null;
        var errors = await _settings.UpdateAsync(current => current with { AutoStopAt = local }).ConfigureAwait(false);
        if (errors.Count > 0)
            return errors;

        _log.Info(local is { } value
            ? $"自動停止を {value:yyyy/MM/dd HH:mm} に予約しました。"
            : "自動停止の予約を解除しました。");
        return errors;
    }
}
