namespace KeepAlive.Core.Configuration;

/// <summary>
/// ユーザー設定ではない、アプリ組み込みの動作パラメータ。
/// </summary>
public sealed class KeepAliveOptions
{
    /// <summary>メモリ上に保持するログの最大件数。</summary>
    public int LogCapacity { get; set; } = 500;

    /// <summary>ログファイルの保存日数。これより古いファイルは自動で削除する。</summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>
    /// スリープ復帰を検知してから即時実行するまでの待ち時間。
    /// 復帰直後はネットワークアダプタの再接続が完了していないことが多いため少し待つ。
    /// </summary>
    public TimeSpan ResumeDelay { get; set; } = TimeSpan.FromSeconds(5);
}
