using System.Text;
using System.Threading.Channels;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using KeepAlive.Infrastructure.Files;

namespace KeepAlive.Infrastructure.History;

/// <summary>
/// 通信の結果の履歴を、日付ごとの CSV ファイル (例: TetherKeeper_20261124.csv) に追記する。
/// <list type="bullet">
/// <item>書き込みはバックグラウンドで行い、<see cref="Append"/> はキューに積むだけですぐ戻る</item>
/// <item>新しいファイルには見出しの行と、Excel で文字化けしないよう UTF-8 の BOM を付ける</item>
/// <item>保存日数を過ぎたファイルは、書き込むときに日付が変わっていれば削除する</item>
/// </list>
/// </summary>
public sealed class CsvResultHistoryStore : IResultHistoryStore, IDisposable
{
    private const string FileExtension = ".csv";

    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    // 結果と、フラッシュ完了を知らせる TaskCompletionSource の両方を同じキューで扱う
    private readonly Channel<object> _queue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _directory;
    private readonly string _filePrefix;
    private readonly int _retentionDays;
    private readonly TimeProvider _timeProvider;
    private readonly Task _processing;
    private DateOnly? _lastCleanupDate;

    /// <param name="directory">履歴のファイルを置くフォルダ (無ければ作成する)。</param>
    /// <param name="filePrefix">ファイル名の接頭辞 (例: "TetherKeeper" → TetherKeeper_20261124.csv)。</param>
    /// <param name="retentionDays">保存日数。今日を含めてこの日数より古いファイルを削除する。</param>
    public CsvResultHistoryStore(string directory, string filePrefix, int retentionDays, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);
        _directory = directory;
        _filePrefix = filePrefix;
        _retentionDays = Math.Max(1, retentionDays);
        _timeProvider = timeProvider;
        _processing = Task.Run(ProcessQueueAsync);
    }

    public void Append(KeepAliveResult result) => _queue.Writer.TryWrite(result);

    public Task FlushAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _queue.Writer.TryWrite(completion) ? completion.Task : Task.CompletedTask;
    }

    public async Task<IReadOnlyList<KeepAliveResult>> LoadAsync(DateTimeOffset from, CancellationToken cancellationToken = default)
    {
        var zone = _timeProvider.LocalTimeZone;
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, zone).DateTime);
        var results = new List<KeepAliveResult>();

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var (path, _) in DailyFiles.Enumerate(_directory, _filePrefix, FileExtension)
                .Where(f => f.Date >= firstDate)
                .OrderBy(f => f.Date))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Utf8WithBom, detectEncodingFromByteOrderMarks: true);
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    // 見出しの行や壊れた行は読み飛ばす
                    if (ResultCsv.Parse(line, zone) is { } result && result.Timestamp >= from)
                        results.Add(result);
                }
            }
        }
        finally
        {
            _fileLock.Release();
        }

        return results;
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _processing.Wait(TimeSpan.FromSeconds(3));
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<KeepAliveResult>();
        var flushRequests = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            // 溜まっている分をまとめて取り出し、ファイルを開く回数を減らす
            while (_queue.Reader.TryRead(out var item))
            {
                if (item is KeepAliveResult result)
                    batch.Add(result);
                else if (item is TaskCompletionSource flush)
                    flushRequests.Add(flush);
            }

            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                WriteBatch(batch);
            }
            finally
            {
                _fileLock.Release();
            }

            batch.Clear();
            foreach (var flush in flushRequests)
                flush.TrySetResult();
            flushRequests.Clear();
        }
    }

    private void WriteBatch(List<KeepAliveResult> results)
    {
        try
        {
            var zone = _timeProvider.LocalTimeZone;
            CleanupIfDateChanged(zone);
            if (results.Count == 0)
                return;

            Directory.CreateDirectory(_directory);

            // 日付をまたいだ場合に備え、結果の日時 (PC の時刻) の日付ごとにファイルを分ける
            foreach (var group in results.GroupBy(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.Timestamp, zone).DateTime)))
            {
                var path = DailyFiles.GetPath(_directory, _filePrefix, FileExtension, group.Key);
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                // 追記のときは BOM を書かない (StreamWriter は、ファイルの先頭のときだけ BOM を書く)
                using var writer = new StreamWriter(stream, Utf8WithBom);
                if (stream.Position == 0)
                    writer.WriteLine(ResultCsv.Header);
                foreach (var result in group)
                    writer.WriteLine(ResultCsv.Format(result, zone));
            }
        }
        catch (Exception ex)
        {
            // 履歴の書き込みに失敗しても、キープアライブは続ける
            System.Diagnostics.Debug.WriteLine($"CsvResultHistoryStore: 履歴の書き込みに失敗しました: {ex}");
        }
    }

    private void CleanupIfDateChanged(TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), zone).DateTime);
        if (_lastCleanupDate == today)
            return;
        _lastCleanupDate = today;
        DailyFiles.DeleteOlderThan(_directory, _filePrefix, FileExtension, today, _retentionDays);
    }
}
