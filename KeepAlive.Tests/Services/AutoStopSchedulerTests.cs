using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace KeepAlive.Tests.Services;

public class AutoStopSchedulerTests
{
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone("Test+9", TimeSpan.FromHours(9), "Test+9", "Test+9");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 1, 23, 45, TimeSpan.Zero));
    private readonly InMemorySettingsStore _store = new();
    private readonly LogService _log;
    private readonly SettingsService _settings;
    private readonly AutoStopScheduler _scheduler;

    public AutoStopSchedulerTests()
    {
        _time.SetLocalTimeZone(Tokyo);
        _log = TestHelpers.CreateLogService(_time);
        _settings = new SettingsService(_store, _log);
        _settings.LoadAsync().GetAwaiter().GetResult();
        _scheduler = new AutoStopScheduler(_settings, _log, _time);
    }

    [Fact]
    public async Task ScheduleAfter_RoundsUpSeconds_AndSavesInLocalTime()
    {
        // 10:23:45 (+09:00) の 1 時間後 → 秒を切り上げて 11:24
        var errors = await _scheduler.ScheduleAfterAsync(TimeSpan.FromHours(1));

        Assert.Empty(errors);
        var expected = new DateTimeOffset(2026, 11, 24, 11, 24, 0, TimeSpan.FromHours(9));
        Assert.Equal(expected, _settings.Current.AutoStopAt);
        Assert.Equal(TimeSpan.FromHours(9), _settings.Current.AutoStopAt!.Value.Offset);
        Assert.Equal(expected, _scheduler.ScheduledAt);
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("2026/11/24 11:24 に予約しました"));
    }

    [Fact]
    public async Task ScheduleAfter_ReplacesExistingSchedule()
    {
        await _scheduler.ScheduleAfterAsync(TimeSpan.FromHours(1));

        await _scheduler.ScheduleAfterAsync(TimeSpan.FromHours(2));

        Assert.Equal(new DateTimeOffset(2026, 11, 24, 12, 24, 0, TimeSpan.FromHours(9)), _scheduler.ScheduledAt);
    }

    [Fact]
    public async Task ScheduleAt_RejectsPastTime()
    {
        var errors = await _scheduler.ScheduleAtAsync(_time.GetUtcNow().AddMinutes(-1));

        Assert.Contains(errors, e => e.Contains("現在より後"));
        Assert.Null(_settings.Current.AutoStopAt);
    }

    [Fact]
    public async Task Cancel_ClearsSchedule()
    {
        await _scheduler.ScheduleAfterAsync(TimeSpan.FromHours(1));

        await _scheduler.CancelAsync();

        Assert.Null(_settings.Current.AutoStopAt);
        Assert.Null(_scheduler.ScheduledAt);
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("予約を解除しました"));
    }

    [Fact]
    public async Task ScheduledAt_IsNull_AfterTimeHasPassed()
    {
        await _scheduler.ScheduleAfterAsync(TimeSpan.FromHours(1));

        _time.Advance(TimeSpan.FromHours(2));

        Assert.Null(_scheduler.ScheduledAt);
    }

    [Fact]
    public void RoundUpToMinute_KeepsExactMinute()
    {
        var exact = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(exact, AutoStopScheduler.RoundUpToMinute(exact));
        Assert.Equal(exact.AddMinutes(1), AutoStopScheduler.RoundUpToMinute(exact.AddTicks(1)));
    }
}
