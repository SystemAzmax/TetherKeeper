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

    /// <summary>
    /// キープアライブ動作中に通信量を計測する間隔。上限に達してから停止するまでの遅れ (上限を超える分) は、この間隔の分までになる。
    /// </summary>
    public TimeSpan DataUsageSampleInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 動作中に通信量の累計を保存する間隔。停止時とリセット時は、この間隔によらず保存する。
    /// </summary>
    public TimeSpan DataUsageSaveInterval { get; set; } = TimeSpan.FromMinutes(1);
}
