namespace KeepAlive.Core.Models;

/// <summary>
/// 1 回のキープアライブ通信の結果。
/// </summary>
public sealed record KeepAliveResult
{
    public required bool Success { get; init; }

    public required string StrategyName { get; init; }

    /// <summary>通信先 (ホスト名、URL、host:port など)。</summary>
    public required string Target { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>通信に要した時間。失敗時はタイムアウトまでの経過時間など。</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>補足情報 (応答ステータス、エラー内容など)。</summary>
    public string? Message { get; init; }

    /// <summary>
    /// 通信方式で失敗したあと、予備の通信方式 (<see cref="AppSettings.FallbackMethod"/>) でやり直した結果か。
    /// このとき <see cref="StrategyName"/> などは予備の方式のもの。
    /// </summary>
    public bool UsedFallback { get; init; }

    public static KeepAliveResult Succeeded(
        string strategyName, string target, DateTimeOffset timestamp, TimeSpan elapsed, string? message = null) =>
        new()
        {
            Success = true,
            StrategyName = strategyName,
            Target = target,
            Timestamp = timestamp,
            Elapsed = elapsed,
            Message = message,
        };

    public static KeepAliveResult Failed(
        string strategyName, string target, DateTimeOffset timestamp, TimeSpan elapsed, string message) =>
        new()
        {
            Success = false,
            StrategyName = strategyName,
            Target = target,
            Timestamp = timestamp,
            Elapsed = elapsed,
            Message = message,
        };
}
