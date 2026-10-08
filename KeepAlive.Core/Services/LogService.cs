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

#pragma warning disable CA2254 // ログメッセージはユーザー向け文字列をそのまま出力する
        _logger.Log(ToLogLevel(level), message);
#pragma warning restore CA2254
        foreach (var sink in _sinks)
            sink.Write(entry);
        EntryAdded?.Invoke(this, entry);
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

    private static LogLevel ToLogLevel(LogLevelKind level) => level switch
    {
        LogLevelKind.Warning => LogLevel.Warning,
        LogLevelKind.Error => LogLevel.Error,
        _ => LogLevel.Information,
    };
}
