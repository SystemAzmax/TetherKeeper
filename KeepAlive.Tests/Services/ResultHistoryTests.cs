using System.Text;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using KeepAlive.Infrastructure.History;
using Microsoft.Extensions.Time.Testing;

namespace KeepAlive.Tests.Services;

public sealed class ResultHistoryTests : IDisposable
{
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone("Test+9", TimeSpan.FromHours(9), "Test+9", "Test+9");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KeepAliveTests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 1, 0, 0, TimeSpan.Zero)); // 10:00 (+09:00)

    public ResultHistoryTests() => _time.SetLocalTimeZone(Tokyo);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static DateTimeOffset Local(int day, int hour, int minute = 0) =>
        new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(9));

    private static KeepAliveResult Result(DateTimeOffset at, bool success, string? message = null, bool fallback = false) =>
        (success
            ? KeepAliveResult.Succeeded("Ping", "1.1.1.1", at, TimeSpan.FromMilliseconds(12), message)
            : KeepAliveResult.Failed("Ping", "1.1.1.1", at, TimeSpan.FromMilliseconds(3000), message ?? "タイムアウト"))
        with { UsedFallback = fallback };

    private CsvResultHistoryStore CreateStore(int retentionDays = 30) => new(_directory, "TetherKeeper", retentionDays, _time);

    [Fact]
    public void Csv_RoundTrips_IncludingCommasQuotesAndFallback()
    {
        var result = Result(Local(24, 9, 5), success: true, message: "応答 \"12\" ms, OK", fallback: true);

        var line = ResultCsv.Format(result, Tokyo);
        var parsed = ResultCsv.Parse(line, Tokyo);

        Assert.StartsWith("2026/11/24 09:05:00,成功,Ping,1.1.1.1,12,予備で成功,", line);
        Assert.Equal(result, parsed);
    }

    [Fact]
    public void Csv_ReplacesNewlinesWithSpaces()
    {
        var line = ResultCsv.Format(Result(Local(24, 9), success: false, message: "1 行目\r\n2 行目"), Tokyo);

        Assert.DoesNotContain('\n', line);
        Assert.Equal("1 行目 2 行目", ResultCsv.Parse(line, Tokyo)?.Message);
    }

    [Theory]
    [InlineData(ResultCsv.Header)]
    [InlineData("")]
    [InlineData("2026/11/24 09:00:00,成功,Ping")]
    [InlineData("2026/11/24 09:00:00,不明,Ping,1.1.1.1,12,,")]
    [InlineData("\"閉じていない,成功,Ping,1.1.1.1,12,,")]
    public void Csv_IgnoresHeaderAndBrokenLines(string line)
    {
        Assert.Null(ResultCsv.Parse(line, Tokyo));
    }

    [Fact]
    public void Summarize_CountsFailuresByLocalHour()
    {
        var results = new[]
        {
            Result(Local(24, 9, 0), success: true),
            Result(Local(24, 9, 30), success: false),
            Result(Local(23, 9, 10), success: false),
            Result(Local(24, 22, 0), success: true, fallback: true),
        };

        var summary = ResultHistory.Summarize(results, Tokyo);

        Assert.Equal(4, summary.Total);
        Assert.Equal(2, summary.Successes);
        Assert.Equal(2, summary.Failures);
        Assert.Equal(1, summary.FallbackSuccesses);
        Assert.Equal(24, summary.ByHour.Count);
        Assert.Equal(new HourlyCount(9, 3, 2), summary.ByHour[9]);
        Assert.Equal(new HourlyCount(22, 1, 0), summary.ByHour[22]);
        Assert.Equal(0, summary.ByHour[0].FailureRate);
    }

    [Fact]
    public async Task Store_AppendsToDailyFiles_WithHeaderOnce_AndLoadsFromTime()
    {
        using (var store = CreateStore())
        {
            store.Append(Result(Local(23, 23, 50), success: true));
            store.Append(Result(Local(24, 0, 10), success: false));
            await store.FlushAsync();
            store.Append(Result(Local(24, 9, 0), success: true));
            await store.FlushAsync();
        }

        // 日付ごとのファイルに分かれ、見出しはファイルごとに 1 回だけ
        var today = await File.ReadAllTextAsync(Path.Combine(_directory, "TetherKeeper_20261124.csv"));
        Assert.Equal(1, today.Split(ResultCsv.Header).Length - 1);
        Assert.True(File.Exists(Path.Combine(_directory, "TetherKeeper_20261123.csv")));

        // Excel で開けるよう、先頭に BOM を付ける
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, "TetherKeeper_20261124.csv"));
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);

        using var reader = CreateStore();
        var loaded = await reader.LoadAsync(Local(24, 0, 0));
        Assert.Equal(2, loaded.Count);
        Assert.False(loaded[0].Success);
        Assert.Equal(Local(24, 9, 0), loaded[1].Timestamp);
    }

    [Fact]
    public async Task Store_DeletesFilesOlderThanRetention()
    {
        Directory.CreateDirectory(_directory);
        var old = Path.Combine(_directory, "TetherKeeper_20261101.csv");
        var kept = Path.Combine(_directory, "TetherKeeper_20261120.csv");
        await File.WriteAllTextAsync(old, ResultCsv.Header);
        await File.WriteAllTextAsync(kept, ResultCsv.Header);

        using var store = CreateStore(retentionDays: 7);
        store.Append(Result(Local(24, 10), success: true));
        await store.FlushAsync();

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(kept));
    }

    [Fact]
    public async Task ResultHistory_RecordsWorkerResults_AndLoadsPeriod()
    {
        var worker = new FakeKeepAliveWorker();
        using var store = CreateStore();
        using var history = new ResultHistory(worker, store, _time);

        worker.RaiseResult(Result(Local(24, 8, 0), success: true));   // 2 時間前
        worker.RaiseResult(Result(Local(24, 9, 30), success: false)); // 30 分前

        var lastHour = await history.LoadAsync(TimeSpan.FromHours(1));

        Assert.Single(lastHour);
        Assert.False(lastHour[0].Success);
    }

    [Fact]
    public async Task WriteCsv_WritesHeaderAndRowsWithBom()
    {
        using var stream = new MemoryStream();

        await ResultHistory.WriteCsvAsync([Result(Local(24, 9), success: true)], stream, Tokyo);

        var bytes = stream.ToArray();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ResultCsv.Header, lines[0]);
        Assert.StartsWith("2026/11/24 09:00:00,成功", lines[1]);
    }
}
