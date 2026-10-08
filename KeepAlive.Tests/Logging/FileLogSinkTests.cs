using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using KeepAlive.Infrastructure.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KeepAlive.Tests.Logging;

public sealed class FileLogSinkTests : IDisposable
{
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KeepAliveTests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 3, 0, 0, TimeSpan.Zero));

    public FileLogSinkTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("JST", Jst, "JST", "JST"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private FileLogSink CreateSink(int retentionDays = 30) => new(_directory, "Test", retentionDays, _time);

    private static LogEntry Entry(int year, int month, int day, int hour, string message, LogLevelKind level = LogLevelKind.Info) =>
        new(new DateTimeOffset(year, month, day, hour, 0, 0, Jst), level, message);

    [Fact]
    public async Task WriteAndFlush_AppendsFormattedLinesToDailyFile()
    {
        using var sink = CreateSink();

        sink.Write(Entry(2026, 10, 8, 12, "成功: Ping"));
        sink.Write(Entry(2026, 10, 8, 12, "失敗: Ping", LogLevelKind.Warning));
        await sink.FlushAsync();

        var lines = await File.ReadAllLinesAsync(sink.GetFilePath(new DateOnly(2026, 10, 8)));
        Assert.Equal(
            ["2026-10-08 12:00:00.000 [Info] 成功: Ping", "2026-10-08 12:00:00.000 [Warning] 失敗: Ping"],
            lines);
    }

    [Fact]
    public async Task Write_AppendsToExistingFile()
    {
        using (var first = CreateSink())
        {
            first.Write(Entry(2026, 10, 8, 9, "1 回目"));
            await first.FlushAsync();
        }

        using var second = CreateSink();
        second.Write(Entry(2026, 10, 8, 10, "2 回目"));
        await second.FlushAsync();

        var lines = await File.ReadAllLinesAsync(second.GetFilePath(new DateOnly(2026, 10, 8)));
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Write_SplitsFilesByEntryDate()
    {
        using var sink = CreateSink();

        sink.Write(Entry(2026, 10, 7, 23, "前日"));
        sink.Write(Entry(2026, 10, 8, 0, "当日"));
        await sink.FlushAsync();

        Assert.Contains("前日", await File.ReadAllTextAsync(sink.GetFilePath(new DateOnly(2026, 10, 7))));
        Assert.Contains("当日", await File.ReadAllTextAsync(sink.GetFilePath(new DateOnly(2026, 10, 8))));
    }

    [Fact]
    public async Task File_CanBeReadWhileSinkIsAlive()
    {
        using var sink = CreateSink();
        sink.Write(Entry(2026, 10, 8, 12, "閲覧テスト"));
        await sink.FlushAsync();

        // メモ帳などで開いている状態を模擬: 共有モードで開いたまま追記できること
        using var reader = new FileStream(
            sink.GetFilePath(new DateOnly(2026, 10, 8)), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        sink.Write(Entry(2026, 10, 8, 13, "追記"));
        await sink.FlushAsync();

        Assert.Contains("追記", await File.ReadAllTextAsync(sink.GetFilePath(new DateOnly(2026, 10, 8))));
    }

    [Fact]
    public async Task Cleanup_DeletesOnlyFilesOlderThanRetention()
    {
        Directory.CreateDirectory(_directory);
        var expired = Path.Combine(_directory, "Test_20261001.log");   // 8 日前 → 削除
        var kept = Path.Combine(_directory, "Test_20261002.log");      // 7 日前 (保存期間内) → 残す
        var unrelated = Path.Combine(_directory, "Other_20200101.log"); // 他のファイル → 残す
        var malformed = Path.Combine(_directory, "Test_backup.log");   // 日付でない → 残す
        foreach (var path in new[] { expired, kept, unrelated, malformed })
            await File.WriteAllTextAsync(path, "x");

        using var sink = CreateSink(retentionDays: 7);
        sink.Write(Entry(2026, 10, 8, 12, "今日"));
        await sink.FlushAsync();

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(kept));
        Assert.True(File.Exists(unrelated));
        Assert.True(File.Exists(malformed));
    }

    [Fact]
    public async Task LogService_ForwardsEntriesToSinks_AndFlushes()
    {
        using var sink = CreateSink();
        var log = new LogService(new KeepAliveOptions(), _time, NullLogger<LogService>.Instance, [sink]);

        log.Info("LogService 経由");
        log.Clear(); // 画面用の消去はファイルに影響しない
        await log.FlushAsync();

        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        Assert.Contains("[Info] LogService 経由", await File.ReadAllTextAsync(sink.GetFilePath(today)));
    }
}
