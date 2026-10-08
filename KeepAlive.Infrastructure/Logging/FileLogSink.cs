using System.Globalization;
using System.Text;
using System.Threading.Channels;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Infrastructure.Logging;

/// <summary>
/// ログを日付ごとのテキストファイル (例: TetherKeeper_20261008.log) に追記する。
/// <list type="bullet">
/// <item>書き込みはバックグラウンドで行い、<see cref="Write"/> はキューに積むだけですぐ戻る</item>
/// <item>ファイルは書き込みのたびに共有モードで開閉するため、アプリ実行中でもメモ帳等で閲覧できる</item>
/// <item>保存日数を過ぎたファイルは起動時と日付が変わったときに削除する</item>
/// </list>
/// </summary>
public sealed class FileLogSink : ILogSink, IDisposable
{
    private const string FileExtension = ".log";
    private const string DateFormat = "yyyyMMdd";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // LogEntry と、フラッシュ完了を知らせる TaskCompletionSource の両方を同じキューで扱う
    // (フラッシュ要求より前に積まれたログが書き終わった時点で完了を通知できる)
    private readonly Channel<object> _queue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
    private readonly string _directory;
    private readonly string _filePrefix;
    private readonly int _retentionDays;
    private readonly TimeProvider _timeProvider;
    private readonly Task _processing;
    private DateOnly? _lastCleanupDate;

    /// <param name="directory">ログファイルを置くフォルダ (無ければ作成する)。</param>
    /// <param name="filePrefix">ファイル名の接頭辞 (例: "TetherKeeper" → TetherKeeper_20261008.log)。</param>
    /// <param name="retentionDays">保存日数。今日を含めてこの日数より古いファイルを削除する。</param>
    public FileLogSink(string directory, string filePrefix, int retentionDays, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);
        _directory = directory;
        _filePrefix = filePrefix;
        _retentionDays = Math.Max(1, retentionDays);
        _timeProvider = timeProvider;
        _processing = Task.Run(ProcessQueueAsync);
    }

    public string Directory => _directory;

    public void Write(LogEntry entry) => _queue.Writer.TryWrite(entry);

    public Task FlushAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _queue.Writer.TryWrite(completion) ? completion.Task : Task.CompletedTask;
    }

    /// <summary>指定日のログファイルのパス。</summary>
    public string GetFilePath(DateOnly date) =>
        Path.Combine(_directory, $"{_filePrefix}_{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{FileExtension}");

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _processing.Wait(TimeSpan.FromSeconds(3));
    }

    private async Task ProcessQueueAsync()
    {
        var batch = new List<LogEntry>();
        var flushRequests = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            // 溜まっている分をまとめて取り出し、ファイルを開く回数を減らす
            while (_queue.Reader.TryRead(out var item))
            {
                if (item is LogEntry entry)
                    batch.Add(entry);
                else if (item is TaskCompletionSource flush)
                    flushRequests.Add(flush);
            }

            WriteBatch(batch);
            batch.Clear();

            foreach (var flush in flushRequests)
                flush.TrySetResult();
            flushRequests.Clear();
        }
    }

    private void WriteBatch(List<LogEntry> entries)
    {
        try
        {
            CleanupIfDateChanged();
            if (entries.Count == 0)
                return;

            System.IO.Directory.CreateDirectory(_directory);

            // 日付をまたいだ場合に備え、ログ自身のタイムスタンプの日付ごとにファイルを分ける
            foreach (var group in entries.GroupBy(e => DateOnly.FromDateTime(e.Timestamp.DateTime)))
            {
                using var stream = new FileStream(
                    GetFilePath(group.Key), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                using var writer = new StreamWriter(stream, Utf8NoBom);
                foreach (var entry in group)
                    writer.WriteLine(Format(entry));
            }
        }
        catch (Exception ex)
        {
            // ログ出力の失敗をログに書くことはできないため、デバッグ出力に留めてアプリは継続する
            System.Diagnostics.Debug.WriteLine($"FileLogSink: ログファイルへの書き込みに失敗しました: {ex}");
        }
    }

    private static string Format(LogEntry entry) =>
        $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}";

    private void CleanupIfDateChanged()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        if (_lastCleanupDate == today)
            return;
        _lastCleanupDate = today;

        if (!System.IO.Directory.Exists(_directory))
            return;

        var oldestKept = today.AddDays(-(_retentionDays - 1));
        foreach (var path in System.IO.Directory.EnumerateFiles(_directory, $"{_filePrefix}_*{FileExtension}"))
        {
            var datePart = Path.GetFileNameWithoutExtension(path)[(_filePrefix.Length + 1)..];
            if (DateOnly.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && date < oldestKept)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // 閲覧中などで消せない場合は次回に持ち越す
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
