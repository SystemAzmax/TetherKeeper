using System.Text;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>1 時間ごと (0〜23 時) の集計。</summary>
/// <param name="Hour">時 (PC の時刻)。</param>
/// <param name="Total">通信した回数。</param>
/// <param name="Failures">失敗した回数。</param>
public sealed record HourlyCount(int Hour, int Total, int Failures)
{
    /// <summary>失敗の割合 (0〜1)。通信していなければ 0。</summary>
    public double FailureRate => Total > 0 ? (double)Failures / Total : 0;
}

/// <summary>期間の集計。</summary>
public sealed record HistorySummary(
    int Total,
    int Successes,
    int Failures,
    int FallbackSuccesses,
    IReadOnlyList<HourlyCount> ByHour)
{
    public double FailureRate => Total > 0 ? (double)Failures / Total : 0;
}

/// <summary>
/// 通信の結果の履歴。Worker の結果を保存先 (<see cref="IResultHistoryStore"/>) に記録し、
/// 期間を指定した集計と CSV への書き出しを行う。
/// </summary>
public sealed class ResultHistory : IDisposable
{
    private readonly IKeepAliveWorker _worker;
    private readonly IResultHistoryStore _store;
    private readonly TimeProvider _timeProvider;

    /// <summary>作った時点から、Worker の結果を記録する。</summary>
    public ResultHistory(IKeepAliveWorker worker, IResultHistoryStore store, TimeProvider timeProvider)
    {
        _worker = worker;
        _store = store;
        _timeProvider = timeProvider;
        _worker.ResultReceived += OnResultReceived;
    }

    /// <summary>直近の <paramref name="period"/> の結果を、古い順に読む。</summary>
    public async Task<IReadOnlyList<KeepAliveResult>> LoadAsync(TimeSpan period, CancellationToken cancellationToken = default)
    {
        // 記録待ちの結果も含めるため、書き終えてから読む
        await _store.FlushAsync().ConfigureAwait(false);
        return await _store.LoadAsync(_timeProvider.GetUtcNow() - period, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>結果を集計する。時間帯は <paramref name="zone"/> の時刻で分ける。</summary>
    public static HistorySummary Summarize(IEnumerable<KeepAliveResult> results, TimeZoneInfo zone)
    {
        var totals = new int[24];
        var failures = new int[24];
        int total = 0, successes = 0, fallbackSuccesses = 0;

        foreach (var result in results)
        {
            var hour = TimeZoneInfo.ConvertTime(result.Timestamp, zone).Hour;
            totals[hour]++;
            total++;
            if (result.Success)
            {
                successes++;
                if (result.UsedFallback)
                    fallbackSuccesses++;
            }
            else
            {
                failures[hour]++;
            }
        }

        var byHour = Enumerable.Range(0, 24).Select(h => new HourlyCount(h, totals[h], failures[h])).ToList();
        return new HistorySummary(total, successes, total - successes, fallbackSuccesses, byHour);
    }

    /// <summary>結果を CSV (見出し付き。Excel で開けるよう UTF-8 の BOM 付き) に書き出す。</summary>
    public static async Task WriteCsvAsync(IEnumerable<KeepAliveResult> results, Stream stream, TimeZoneInfo zone)
    {
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(ResultCsv.Header).ConfigureAwait(false);
        foreach (var result in results)
            await writer.WriteLineAsync(ResultCsv.Format(result, zone)).ConfigureAwait(false);
    }

    public void Dispose() => _worker.ResultReceived -= OnResultReceived;

    private void OnResultReceived(object? sender, KeepAliveResult result) => _store.Append(result);
}
