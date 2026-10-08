using KeepAlive.Core.Models;
using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Settings;

public class SettingsServiceTests
{
    private readonly InMemorySettingsStore _store = new();
    private readonly LogService _log = TestHelpers.CreateLogService();
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _service = new SettingsService(_store, _log);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenNothingStored()
    {
        var loaded = await _service.LoadAsync();

        Assert.Equal(new AppSettings(), loaded);
        Assert.Equal(new AppSettings(), _service.Current);
    }

    [Fact]
    public async Task LoadAsync_NormalizesStoredSettings()
    {
        _store.Stored = new AppSettings { IntervalSeconds = 0 };

        var loaded = await _service.LoadAsync();

        Assert.Equal(AppSettings.MinIntervalSeconds, loaded.IntervalSeconds);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToDefaultsAndLogs_WhenStoreFails()
    {
        _store.LoadException = new IOException("disk error");

        var loaded = await _service.LoadAsync();

        Assert.Equal(new AppSettings(), loaded);
        Assert.Contains(_log.GetEntries(), e => e.Level == LogLevelKind.Warning && e.Message.Contains("disk error"));
    }

    [Fact]
    public async Task LoadAsync_RaisesSettingsChanged_WhenValuesDiffer()
    {
        _store.Stored = new AppSettings { IntervalSeconds = 60 };
        AppSettings? raised = null;
        _service.SettingsChanged += (_, s) => raised = s;

        await _service.LoadAsync();

        Assert.Equal(60, raised?.IntervalSeconds);
    }

    [Fact]
    public async Task SaveAsync_PersistsAndUpdatesCurrent_WhenValid()
    {
        var settings = new AppSettings { Method = KeepAliveMethod.HttpHead, IntervalSeconds = 45 };
        AppSettings? raised = null;
        _service.SettingsChanged += (_, s) => raised = s;

        var errors = await _service.SaveAsync(settings);

        Assert.Empty(errors);
        Assert.Equal(settings, _store.Stored);
        Assert.Equal(settings, _service.Current);
        Assert.Equal(settings, raised);
    }

    [Fact]
    public async Task SaveAsync_ReturnsErrorsAndDoesNotPersist_WhenInvalid()
    {
        var errors = await _service.SaveAsync(new AppSettings { IntervalSeconds = 1 });

        Assert.NotEmpty(errors);
        Assert.Equal(0, _store.SaveCount);
        Assert.Equal(new AppSettings(), _service.Current);
    }

    [Fact]
    public async Task SaveAsync_ReturnsErrorAndKeepsCurrent_WhenStoreFails()
    {
        _store.SaveException = new UnauthorizedAccessException("denied");

        var errors = await _service.SaveAsync(new AppSettings { IntervalSeconds = 45 });

        Assert.Contains(errors, e => e.Contains("denied"));
        Assert.Equal(new AppSettings(), _service.Current);
    }
}
