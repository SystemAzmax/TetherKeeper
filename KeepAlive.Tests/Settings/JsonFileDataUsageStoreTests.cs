using KeepAlive.Core.Models;
using KeepAlive.Infrastructure.Settings;

namespace KeepAlive.Tests.Settings;

public sealed class JsonFileDataUsageStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KeepAliveTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "sub", "data-usage.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenFileMissing()
    {
        var store = new JsonFileDataUsageStore(FilePath);

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        var store = new JsonFileDataUsageStore(FilePath);
        var usage = new DataUsage(123_456_789, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(9)));

        await store.SaveAsync(usage);
        var loaded = await new JsonFileDataUsageStore(FilePath).LoadAsync();

        Assert.Equal(usage, loaded);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }
}
