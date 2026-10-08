using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// キープアライブ通信 1 回分の実行方式 (Strategy パターン)。
/// </summary>
public interface IKeepAliveStrategy
{
    /// <summary>表示用の方式名。</summary>
    string Name { get; }

    /// <summary>この Strategy が担当する方式。設定値からの選択に使う。</summary>
    KeepAliveMethod Method { get; }

    /// <summary>
    /// 通信を 1 回実行する。通信失敗は例外ではなく <see cref="KeepAliveResult.Success"/> = false で返す。
    /// <paramref name="cancellationToken"/> によるキャンセル時のみ <see cref="OperationCanceledException"/> を送出する。
    /// </summary>
    Task<KeepAliveResult> ExecuteAsync(CancellationToken cancellationToken);
}
