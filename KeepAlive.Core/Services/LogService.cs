using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using Microsoft.Extensions.Logging;

namespace KeepAlive.Core.Services;

/// <summary>
/// 直近のログをリングバッファで保持し、同時に <see cref="ILogger"/> と各 <see cref="ILogSink"/> (ファイル等) にも出力する。
/// </summary>
public sealed class LogService : ILogService
{
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new();
    private readonly int _capacity;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LogService> _logger;
    private readonly ILogSink[] _sinks;

    public LogService(
        KeepAliveOptions options, TimeProvider timeProvider, ILogger<LogService> logger, IEnumerable<ILogSink> sinks)
    {
        _capacity = Math.Max(1, options.LogCapacity);
        _timeProvider = timeProvider;
        _logger = logger;
        _sinks = sinks.ToArray();
    }

    public event EventHandler<LogEntry>? EntryAdded;

    public event EventHandler? Cleared;

    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    public void Log(LogLevelKind level, string message)
    {
        var entry = new LogEntry(_timeProvider.GetLocalNow(), level, message);
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity)
                _entries.Dequeue();
        }

        // ログ出力の失敗で呼び出し元 (キープアライブのループ等) を止めないよう、出力先ごとに例外を握りつぶす。
        // 失敗をこのログ自身に書くと再帰するため、ILogger (デバッグ出力) にだけ残す
#pragma warning disable CA2254 // ログメッセージはユーザー向け文字列をそのまま出力する
        TryOutput("ILogger", () => _logger.Log(ToLogLevel(level), message));
#pragma warning restore CA2254
        foreach (var sink in _sinks)
            TryOutput(sink.GetType().Name, () => sink.Write(entry));
        TryOutput(nameof(EntryAdded), () => EntryAdded?.Invoke(this, entry));
    }

    public Task FlushAsync() => Task.WhenAll(_sinks.Select(s => s.FlushAsync()));

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }

        Cleared?.Invoke(this, EventArgs.Empty);
    }

    private void TryOutput(string target, Action output)
    {
        try
        {
            output();
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogError(ex, "ログの出力に失敗しました ({Target})", target);
            }
            catch
            {
                // ILogger 自体が失敗した場合は諦める
            }
        }
    }

    private static LogLevel ToLogLevel(LogLevelKind level) => level switch
    {
        LogLevelKind.Warning => LogLevel.Warning,
        LogLevelKind.Error => LogLevel.Error,
        _ => LogLevel.Information,
    };
}
