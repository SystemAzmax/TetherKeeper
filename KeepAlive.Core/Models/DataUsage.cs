namespace KeepAlive.Core.Models;

/// <summary>
/// 通信量の累計。リセットするまで、キープアライブの停止やアプリの再起動をまたいで加算する。
/// </summary>
/// <param name="TotalBytes">送受信の合計 (バイト)。</param>
/// <param name="Since">数え始めた日時 (リセットした日時、または初めて計測した日時)。まだ数えていなければ null。</param>
public sealed record DataUsage(long TotalBytes, DateTimeOffset? Since)
{
    public static DataUsage Empty { get; } = new(0, null);

    /// <summary>
    /// バイト数を「12.3 MB」の形にする。上限 (<see cref="AppSettings.DataLimitMegabytes"/>) と単位をそろえるため、
    /// 1 KB = 1,000 バイトとする。
    /// </summary>
    public static string Format(long bytes) => bytes switch
    {
        < AppSettings.BytesPerMegabyte => $"{bytes / 1_000.0:F1} KB",
        < 1_000 * AppSettings.BytesPerMegabyte => $"{bytes / (double)AppSettings.BytesPerMegabyte:F1} MB",
        _ => $"{bytes / (1_000.0 * AppSettings.BytesPerMegabyte):F2} GB",
    };
}
