using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// ログの出力先 (ファイルなど)。<see cref="ILogService"/> に記録されたログがすべて渡される。
/// </summary>
public interface ILogSink
{
    /// <summary>
    /// ログを 1 件出力する。呼び出し元 (キープアライブ処理) を待たせないよう、すぐに戻ること。
    /// </summary>
    void Write(LogEntry entry);

    /// <summary>未出力のログをすべて書き出す (アプリ終了時など)。</summary>
    Task FlushAsync();
}
