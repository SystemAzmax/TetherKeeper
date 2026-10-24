using KeepAlive.Core.Configuration;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace KeepAlive.Tests.Services;

public sealed class DataUsageTrackerTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeDataUsageMeter _meter = new();
    private readonly InMemoryDataUsageStore _store = new();
    private readonly LogService _log;
    private readonly DataUsageTracker _tracker;

    public DataUsageTrackerTests()
    {
        _log = TestHelpers.CreateLogService(_time);
        _tracker = new DataUsageTracker(_meter, _store, _log, _time, new KeepAliveOptions());
    }

    private static DataUsageReading Reading(string interfaceId, long totalBytes) => new(interfaceId, interfaceId, totalBytes);

    [Fact]
    public async Task Measurement_AddsIncreaseSinceStart_AndSavesOnDispose()
    {
        await _tracker.LoadAsync();
        // 開始前の通信は数えない
        _meter.Reading = Reading("wifi", 10_000);

        var measurement = _tracker.StartMeasurement();
        _meter.AddBytes(1_500);
        await measurement.SampleAsync();
        _meter.AddBytes(500);
        await measurement.DisposeAsync();

        Assert.Equal(2_000, _tracker.Current.TotalBytes);
        Assert.Equal(2_000, _store.Stored?.TotalBytes);
    }

    [Fact]
    public async Task Measurement_UsesNewAdapterAsBaseline_WhenAdapterChanges()
    {
        _meter.Reading = Reading("ethernet", 1_000);
        var measurement = _tracker.StartMeasurement();

        _meter.Reading = Reading("wifi", 50_000);
        await measurement.SampleAsync();
        _meter.AddBytes(100);
        await measurement.SampleAsync();

        Assert.Equal(100, _tracker.Current.TotalBytes);
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("通信量の計測対象: wifi"));
    }

    [Fact]
    public async Task Measurement_TreatsDecreaseAsCounterReset()
    {
        _meter.Reading = Reading("wifi", 1_000);
        var measurement = _tracker.StartMeasurement();

        _meter.Reading = Reading("wifi", 300);
        await measurement.SampleAsync();

        Assert.Equal(300, _tracker.Current.TotalBytes);
    }

    [Fact]
    public async Task Measurement_KeepsBaseline_WhileNoNetwork()
    {
        _meter.Reading = Reading("wifi", 1_000);
        var measurement = _tracker.StartMeasurement();

        _meter.Reading = null;
        await measurement.SampleAsync();
        _meter.Reading = Reading("wifi", 1_200);
        await measurement.SampleAsync();

        Assert.Equal(200, _tracker.Current.TotalBytes);
    }

    [Fact]
    public async Task Measurement_ContinuesAfterReadError_AndLogsOnce()
    {
        _meter.Reading = Reading("wifi", 1_000);
        var measurement = _tracker.StartMeasurement();

        _meter.ThrowOnRead = new InvalidOperationException("meter boom");
        await measurement.SampleAsync();
        await measurement.SampleAsync();
        _meter.ThrowOnRead = null;
        _meter.Reading = Reading("wifi", 1_100);
        await measurement.SampleAsync();

        Assert.Equal(100, _tracker.Current.TotalBytes);
        Assert.Single(_log.GetEntries(), e => e.Level == LogLevelKind.Warning && e.Message.Contains("meter boom"));
    }

    [Fact]
    public async Task LoadAsync_AddsBytesMeasuredBeforeLoad_AndDoesNotSaveBeforeLoad()
    {
        var since = _time.GetLocalNow().AddDays(-1);
        _store.Stored = new DataUsage(5_000, since);
        var measurement = _tracker.StartMeasurement();
        _meter.AddBytes(700);
        await measurement.DisposeAsync();

        // 読み込む前に保存すると、保存済みの累計を消してしまう
        Assert.Equal(0, _store.SaveCount);

        await _tracker.LoadAsync();

        Assert.Equal(5_700, _tracker.Current.TotalBytes);
        Assert.Equal(since, _tracker.Current.Since);
    }

    [Fact]
    public async Task LoadAsync_TreatsNegativeStoredTotalAsZero()
    {
        _store.Stored = new DataUsage(-100, null);

        await _tracker.LoadAsync();

        Assert.Equal(0, _tracker.Current.TotalBytes);
    }

    [Fact]
    public async Task StartMeasurement_SetsSince_WhenNeverMeasured()
    {
        await _tracker.LoadAsync();
        Assert.Null(_tracker.Current.Since);

        await using (_tracker.StartMeasurement())
        {
        }

        Assert.Equal(_time.GetLocalNow(), _tracker.Current.Since);
        Assert.Equal(_time.GetLocalNow(), _store.Stored?.Since);
    }

    [Fact]
    public async Task ResetAsync_ZeroesTotalAndSaves_AndKeepsMeasuring()
    {
        await _tracker.LoadAsync();
        var measurement = _tracker.StartMeasurement();
        _meter.AddBytes(1_000);
        await measurement.SampleAsync();
        _time.Advance(TimeSpan.FromHours(1));

        await _tracker.ResetAsync();

        Assert.Equal(0, _tracker.Current.TotalBytes);
        Assert.Equal(_time.GetLocalNow(), _tracker.Current.Since);
        Assert.Equal(0, _store.Stored?.TotalBytes);

        _meter.AddBytes(200);
        await measurement.SampleAsync();
        Assert.Equal(200, _tracker.Current.TotalBytes);
    }

    [Fact]
    public async Task ResetBeforeLoad_IsNotOverwrittenByStoredTotal()
    {
        _store.Stored = new DataUsage(5_000, null);

        await _tracker.ResetAsync();
        await _tracker.LoadAsync();

        Assert.Equal(0, _tracker.Current.TotalBytes);
    }

    [Fact]
    public async Task SampleAsync_SavesAtMostOncePerSaveInterval()
    {
        await _tracker.LoadAsync();
        var measurement = _tracker.StartMeasurement();
        _meter.AddBytes(100);
        await measurement.SampleAsync();
        var saveCount = _store.SaveCount;

        _meter.AddBytes(100);
        await measurement.SampleAsync();
        Assert.Equal(saveCount, _store.SaveCount);

        _time.Advance(new KeepAliveOptions().DataUsageSaveInterval);
        _meter.AddBytes(100);
        await measurement.SampleAsync();
        Assert.Equal(saveCount + 1, _store.SaveCount);
        Assert.Equal(300, _store.Stored?.TotalBytes);
    }

    [Fact]
    public async Task Changed_IsRaisedWhenTotalIncreases()
    {
        await _tracker.LoadAsync();
        var measurement = _tracker.StartMeasurement();
        var raised = new List<DataUsage>();
        _tracker.Changed += (_, usage) => raised.Add(usage);

        _meter.AddBytes(100);
        await measurement.SampleAsync();
        await measurement.SampleAsync();

        Assert.Single(raised);
        Assert.Equal(100, raised[0].TotalBytes);
    }

    [Theory]
    [InlineData(0, "0.0 KB")]
    [InlineData(12_345, "12.3 KB")]
    [InlineData(12_345_678, "12.3 MB")]
    [InlineData(1_500_000_000, "1.50 GB")]
    public void Format_UsesDecimalUnits(long bytes, string expected)
    {
        Assert.Equal(expected, DataUsage.Format(bytes));
    }
}
