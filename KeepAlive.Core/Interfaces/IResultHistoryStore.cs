using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 通信の結果の履歴の保存先 (実装は Infrastructure。日ごとの CSV ファイル)。
/// ログの文面に頼らずに集計できるよう、結果をログとは別に残す。
/// </summary>
public interface IResultHistoryStore
{
    /// <summary>結果を追加する。書き込みは後で行い、すぐ戻る。</summary>
    void Append(KeepAliveResult result);

    /// <summary>それまでに追加した結果を書き終えるまで待つ。</summary>
    Task FlushAsync();

    /// <summary><paramref name="from"/> 以降の結果を、古い順に読む。</summary>
    Task<IReadOnlyList<KeepAliveResult>> LoadAsync(DateTimeOffset from, CancellationToken cancellationToken = default);
}
