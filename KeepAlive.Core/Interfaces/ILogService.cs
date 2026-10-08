using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 画面表示用のアプリケーションログ。
/// </summary>
public interface ILogService
{
    event EventHandler<LogEntry>? EntryAdded;

    event EventHandler? Cleared;

    /// <summary>保持中のログ (古い順) のスナップショット。</summary>
    IReadOnlyList<LogEntry> GetEntries();

    void Log(LogLevelKind level, string message);

    /// <summary>画面表示用のログを消去する (ファイルに出力済みのログは消さない)。</summary>
    void Clear();

    /// <summary>出力先 (<see cref="ILogSink"/>) に未出力のログを書き出す。</summary>
    Task FlushAsync();
}

public static class LogServiceExtensions
{
    public static void Info(this ILogService log, string message) => log.Log(LogLevelKind.Info, message);

    public static void Warning(this ILogService log, string message) => log.Log(LogLevelKind.Warning, message);

    public static void Error(this ILogService log, string message) => log.Log(LogLevelKind.Error, message);
}
