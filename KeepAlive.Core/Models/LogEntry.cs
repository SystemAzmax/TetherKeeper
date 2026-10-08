namespace KeepAlive.Core.Models;

public enum LogLevelKind
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// 画面表示用のアプリケーションログ 1 件。
/// </summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevelKind Level, string Message)
{
    public override string ToString() => $"{Timestamp:HH:mm:ss} [{Level}] {Message}";
}
