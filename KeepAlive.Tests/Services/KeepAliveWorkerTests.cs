using KeepAlive.Core.Configuration;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace KeepAlive.Tests.Services;

public sealed class KeepAliveWorkerTests : IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _time = new();
    private readonly FakePowerEventSource _power = new();
    private readonly InMemorySettingsStore _store = new();
    private readonly FakeDataUsageMeter _meter = new();
    private readonly InMemoryDataUsageStore _usageStore = new();
    private readonly LogService _log;
    private readonly SettingsService _settings;
    private readonly DataUsageTracker _dataUsage;
    private readonly FakeStrategy _ping;
    private readonly FakeStrategy _tcp;
    private readonly KeepAliveWorker _worker;

    public KeepAliveWorkerTests()
    {
        _log = TestHelpers.CreateLogService(_time);
        _store.Stored = new AppSettings { Method = KeepAliveMethod.Ping, IntervalSeconds = (int)Interval.TotalSeconds };
        _settings = new SettingsService(_store, _log);
        _settings.LoadAsync().GetAwaiter().GetResult();

        var options = new KeepAliveOptions { ResumeDelay = TimeSpan.FromSeconds(5) };
        _dataUsage = new DataUsageTracker(_meter, _usageStore, _log, _time, options);
        _dataUsage.LoadAsync().GetAwaiter().GetResult();

        _ping = new FakeStrategy(KeepAliveMethod.Ping, _time);
        _tcp = new FakeStrategy(KeepAliveMethod.TcpConnect, _time);
        _worker = new KeepAliveWorker(
            new KeepAliveStrategyResolver([_ping, _tcp]),
            _settings,
            _dataUsage,
            _log,
            _power,
            _time,
            options);
    }

    public async ValueTask DisposeAsync()
    {
        await _worker.StopAsync();
        _worker.Dispose();
    }

    private void AdvanceOneSecond() => _time.Advance(TimeSpan.FromSeconds(1));

    [Fact]
    public async Task Start_ExecutesImmediately()
    {
        _worker.Start();

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        Assert.True(_worker.IsRunning);
    }

    [Fact]
    public async Task Start_RepeatsAfterInterval()
    {
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2, AdvanceOneSecond);

        var times = _ping.Executions.ToArray();
        Assert.True(times[1] - times[0] >= Interval, $"間隔が短すぎます: {times[1] - times[0]}");
    }

    [Fact]
    public async Task Start_IsIdempotent()
    {
        _worker.Start();
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount >= 1);
        await Task.Delay(100);

        Assert.Equal(1, _ping.ExecutionCount);
    }

    [Fact]
    public async Task StopAsync_PreventsFurtherExecutions()
    {
        var stateChanges = 0;
        _worker.StateChanged += (_, _) => stateChanges++;
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        await _worker.StopAsync();
        _time.Advance(Interval * 5);
        await Task.Delay(100);

        Assert.False(_worker.IsRunning);
        Assert.Null(_worker.NextRunAt);
        Assert.Equal(1, _ping.ExecutionCount);
        Assert.Equal(2, stateChanges);
    }

    [Fact]
    public async Task Worker_CanBeRestartedAfterStop()
    {
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        await _worker.StopAsync();

        _worker.Start();

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
    }

    [Fact]
    public async Task StrategyException_IsRecordedAsFailure_AndLoopContinues()
    {
        _ping.ThrowOnExecute = new InvalidOperationException("boom");
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _worker.Statistics.FailureCount == 1);

        Assert.Contains("boom", _worker.Statistics.LastResult?.Message);

        _ping.ThrowOnExecute = null;
        await TestHelpers.WaitUntilAsync(() => _worker.Statistics.SuccessCount == 1, AdvanceOneSecond);
        Assert.True(_worker.IsRunning);
    }

    [Fact]
    public async Task Statistics_TrackConsecutiveFailures()
    {
        _ping.NextSuccess = false;
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _worker.Statistics.ConsecutiveFailures == 2, AdvanceOneSecond);

        _ping.NextSuccess = true;
        await TestHelpers.WaitUntilAsync(() => _worker.Statistics.SuccessCount == 1, AdvanceOneSecond);

        var stats = _worker.Statistics;
        Assert.Equal(0, stats.ConsecutiveFailures);
        Assert.Equal(2, stats.FailureCount);
        Assert.Equal(3, stats.TotalCount);
    }

    [Fact]
    public async Task ResultReceived_IsRaisedForEachExecution()
    {
        var results = new List<KeepAliveResult>();
        _worker.ResultReceived += (_, r) => { lock (results) results.Add(r); };

        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => { lock (results) return results.Count == 1; });

        Assert.Equal("FakePing", results[0].StrategyName);
    }

    [Fact]
    public async Task TriggerNow_ExecutesWithoutWaitingForInterval()
    {
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        _worker.TriggerNow();

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
        var times = _ping.Executions.ToArray();
        Assert.Equal(times[0], times[1]);
    }

    [Fact]
    public async Task SettingsChange_SwitchesStrategyImmediately()
    {
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        var errors = await _settings.SaveAsync(_settings.Current with { Method = KeepAliveMethod.TcpConnect });
        Assert.Empty(errors);

        await TestHelpers.WaitUntilAsync(() => _tcp.ExecutionCount == 1);
        Assert.Equal(1, _ping.ExecutionCount);
    }

    [Fact]
    public async Task Resume_TriggersExecutionAfterResumeDelay()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600 });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        _power.RaiseResumed();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2, AdvanceOneSecond);

        var times = _ping.Executions.ToArray();
        var gap = times[1] - times[0];
        Assert.InRange(gap, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60));
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("スリープから復帰"));
    }

    [Fact]
    public async Task LargeTimeGap_IsLoggedAsPossibleSleep()
    {
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        // タイマーが発火するより前に時計が大きく進んだ状況 (スリープ復帰) を模擬
        _time.Advance(TimeSpan.FromHours(1));

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
        Assert.Contains(_log.GetEntries(), e => e.Level == LogLevelKind.Warning && e.Message.Contains("前回実行から"));
    }
    [Fact]
    public async Task AutoStopAt_StopsWorkerAndClearsSetting()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600, AutoStopAt = _time.GetUtcNow().AddSeconds(30) });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning, AdvanceOneSecond);
        await TestHelpers.WaitUntilAsync(() => _settings.Current.AutoStopAt is null);

        Assert.Equal(1, _ping.ExecutionCount);
        Assert.Null(_store.Stored?.AutoStopAt);
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("自動停止日時"));
    }

    [Fact]
    public async Task AutoStopAt_PassedDuringSleep_StopsWithoutExecuting()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600, AutoStopAt = _time.GetUtcNow().AddMinutes(30) });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        _time.Advance(TimeSpan.FromHours(2));
        _power.RaiseResumed();

        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning, AdvanceOneSecond);
        Assert.Equal(1, _ping.ExecutionCount);
    }

    [Fact]
    public async Task AutoStopAt_AlreadyPassedAtStart_IsIgnored()
    {
        await _settings.SaveAsync(_settings.Current with { AutoStopAt = _time.GetUtcNow().AddMinutes(-1) });
        _worker.Start();

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2, AdvanceOneSecond);
        Assert.True(_worker.IsRunning);
    }

    [Fact]
    public async Task AutoStop_DoesNotStopRunRestartedByUser()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600, AutoStopAt = _time.GetUtcNow().AddSeconds(30) });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        await _worker.StopAsync();

        // 自動停止日時の後に再開した実行は、過ぎた日時では止まらない
        _time.Advance(TimeSpan.FromMinutes(1));
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
        _time.Advance(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        Assert.True(_worker.IsRunning);
    }
    [Fact]
    public async Task ScheduleAutoStopWhileRunning_DoesNotExecuteAndStopsAtTime()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600 });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        // 自動停止日時だけの変更では通信しない
        await _settings.SaveAsync(_settings.Current with { AutoStopAt = _time.GetUtcNow().AddSeconds(30) });
        await Task.Delay(100);
        Assert.Equal(1, _ping.ExecutionCount);

        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning, AdvanceOneSecond);
        Assert.Equal(1, _ping.ExecutionCount);
    }

    [Fact]
    public async Task DataLimit_StopsWithoutWaitingForNextRun_AndKeepsLimit()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600, DataLimitMegabytes = 1 });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);

        _meter.AddBytes(AppSettings.BytesPerMegabyte);

        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning, AdvanceOneSecond);
        Assert.Equal(1, _ping.ExecutionCount);
        Assert.Equal(1, _settings.Current.DataLimitMegabytes);
        Assert.Equal(AppSettings.BytesPerMegabyte, _usageStore.Stored?.TotalBytes);
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("上限 (1 MB) に達しました"));
    }

    [Fact]
    public async Task Start_IsRefusedWhileDataLimitReached_AndAllowedAfterReset()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600, DataLimitMegabytes = 1 });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        _meter.AddBytes(AppSettings.BytesPerMegabyte);
        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning, AdvanceOneSecond);

        _worker.Start();
        Assert.False(_worker.IsRunning);
        Assert.Contains(_log.GetEntries(), e => e.Level == LogLevelKind.Warning && e.Message.Contains("開始できません"));

        await _dataUsage.ResetAsync();
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
        Assert.True(_worker.IsRunning);
    }

    [Fact]
    public async Task LoweringDataLimitWhileRunning_StopsWithoutExecuting()
    {
        await _settings.SaveAsync(_settings.Current with { IntervalSeconds = 600 });
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        _meter.AddBytes(2 * AppSettings.BytesPerMegabyte);
        await TestHelpers.WaitUntilAsync(() => _dataUsage.Current.TotalBytes > 0, AdvanceOneSecond);

        // 上限だけの変更では通信しない
        await _settings.SaveAsync(_settings.Current with { DataLimitMegabytes = 1 });

        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning);
        Assert.Equal(1, _ping.ExecutionCount);
    }

    [Fact]
    public async Task DataUsage_IsCountedOnlyWhileRunning()
    {
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        _meter.AddBytes(1_000);
        await _worker.StopAsync();

        _meter.AddBytes(5_000);
        _worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
        await _worker.StopAsync();

        Assert.Equal(1_000, _dataUsage.Current.TotalBytes);
    }

    [Fact]
    public async Task ResultReceivedHandlerException_DoesNotStopLoop()
    {
        _worker.ResultReceived += (_, _) => throw new InvalidOperationException("handler boom");
        _worker.Start();

        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2, AdvanceOneSecond);

        Assert.True(_worker.IsRunning);
        Assert.Contains(_log.GetEntries(), e => e.Level == LogLevelKind.Error && e.Message.Contains("handler boom"));
    }

    [Fact]
    public async Task UnexpectedLoopException_StopsWorkerInsteadOfHangingAsRunning()
    {
        var settings = new FaultySettingsService(new AppSettings { IntervalSeconds = 600 });
        using var worker = new KeepAliveWorker(
            new KeepAliveStrategyResolver([_ping, _tcp]), settings, _dataUsage, _log, _power, _time, new KeepAliveOptions());
        var stateChanges = 0;
        worker.StateChanged += (_, _) => Interlocked.Increment(ref stateChanges);

        worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 1);
        settings.Throw = true;
        worker.TriggerNow();

        await TestHelpers.WaitUntilAsync(() => !worker.IsRunning);
        await TestHelpers.WaitUntilAsync(() => Volatile.Read(ref stateChanges) == 2);
        Assert.Contains(_log.GetEntries(), e => e.Level == LogLevelKind.Error && e.Message.Contains("settings boom"));

        // 停止済みのため StopAsync は何もせず、例外も投げない。再開もできる
        await worker.StopAsync();
        settings.Throw = false;
        worker.Start();
        await TestHelpers.WaitUntilAsync(() => _ping.ExecutionCount == 2);
        await worker.StopAsync();
    }
}
