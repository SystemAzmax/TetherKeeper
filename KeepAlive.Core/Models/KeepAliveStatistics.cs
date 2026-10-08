namespace KeepAlive.Core.Models;

/// <summary>
/// キープアライブ実行の集計値 (ある時点のスナップショット)。
/// </summary>
public sealed record KeepAliveStatistics(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    int ConsecutiveFailures,
    KeepAliveResult? LastResult)
{
    public static KeepAliveStatistics Empty { get; } = new(0, 0, 0, 0, null);

    public KeepAliveStatistics Add(KeepAliveResult result) => result.Success
        ? new(TotalCount + 1, SuccessCount + 1, FailureCount, 0, result)
        : new(TotalCount + 1, SuccessCount, FailureCount + 1, ConsecutiveFailures + 1, result);
}
