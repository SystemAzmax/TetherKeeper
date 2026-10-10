using KeepAlive.Core.Models;
using KeepAlive.Infrastructure.Settings;

namespace KeepAlive.Tests.Settings;

public sealed class JsonFileSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KeepAliveTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenFileMissing()
    {
        var store = new JsonFileSettingsStore(FilePath);

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        var store = new JsonFileSettingsStore(FilePath);
        var settings = new AppSettings
        {
            Method = KeepAliveMethod.TcpConnect,
            IntervalSeconds = 15,
            TcpHost = "example.com",
            TcpPort = 8443,
            StartOnLaunch = true,
            AutoStopAt = new DateTimeOffset(2026, 10, 11, 18, 30, 0, TimeSpan.FromHours(9)),
        };

        await store.SaveAsync(settings);
        var loaded = await new JsonFileSettingsStore(FilePath).LoadAsync();

        Assert.Equal(settings, loaded);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public async Task SaveAsync_WritesEnumAsString()
    {
        var store = new JsonFileSettingsStore(FilePath);

        await store.SaveAsync(new AppSettings { Method = KeepAliveMethod.HttpHead });

        Assert.Contains("\"HttpHead\"", await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultsForMissingProperties()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """{ "IntervalSeconds": 20 }""");

        var loaded = await new JsonFileSettingsStore(FilePath).LoadAsync();

        Assert.Equal(new AppSettings { IntervalSeconds = 20 }, loaded);
    }
}
