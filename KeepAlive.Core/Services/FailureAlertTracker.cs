using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>通知の種類。</summary>
public enum FailureAlertKind
{
    /// <summary>設定した回数だけ続けて失敗した (接続が切れているとみなす)。</summary>
    Failing,

    /// <summary>失敗が続いた状態から、成功に戻った。</summary>
    Recovered,
}

/// <summary>利用者に知らせる、通信の失敗または復旧。</summary>
/// <param name="Kind">通知の種類。</param>
/// <param name="ConsecutiveFailures">連続した失敗の回数 (復旧のときは、成功する直前までの回数)。</param>
/// <param name="FailingSince">失敗が続き始めた日時 (最初に失敗した通信の日時)。</param>
/// <param name="Duration">復旧のときは、失敗が続いた時間 (最初の失敗から成功まで)。失敗のときは null。</param>
public sealed record FailureAlert(
    FailureAlertKind Kind,
    int ConsecutiveFailures,
    DateTimeOffset FailingSince,
    TimeSpan? Duration)
{
    /// <summary>通知のタイトル。</summary>
    public string Title => Kind == FailureAlertKind.Failing
        ? CoreStrings.Alert_FailingTitle
        : CoreStrings.Alert_RecoveredTitle;

    /// <summary>通知の本文。</summary>
    public string Message => Kind == FailureAlertKind.Failing
        ? Text.Format(CoreStrings.Alert_FailingMessage, ConsecutiveFailures, FailingSince.ToLocalTime())
        : Text.Format(CoreStrings.Alert_RecoveredMessage, ConsecutiveFailures, FormatDuration(Duration ?? TimeSpan.Zero));

    /// <summary>失敗が続いた時間を「約 5 分」「約 1 時間 20 分」のように表す。</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
            return CoreStrings.Duration_LessThanMinute;

        var minutes = (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
        if (minutes < 60)
            return Text.Format(CoreStrings.Duration_Minutes, minutes);

        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0
            ? Text.Format(CoreStrings.Duration_Hours, hours)
            : Text.Format(CoreStrings.Duration_HoursMinutes, hours, rest);
    }
}

/// <summary>
/// キープアライブの結果から、通信の失敗が続いたこと・そこから復旧したことを判定する。
/// 同じ失敗の状態では 1 回だけ知らせ、復旧は失敗を知らせたあとだけ知らせる。
/// スレッドセーフではないため、結果は 1 つのスレッドから順に渡すこと。
/// </summary>
public sealed class FailureAlertTracker
{
    private int _consecutiveFailures;
    private DateTimeOffset? _failingSince;

    /// <summary>設定した回数だけ続けて失敗し、まだ成功に戻っていないか (トレイアイコンの切り替えに使う)。</summary>
    public bool IsFailing { get; private set; }

    /// <summary>
    /// 1 回分の結果を反映する。知らせることがあれば、その内容を返す。
    /// </summary>
    /// <param name="result">キープアライブの結果。</param>
    /// <param name="threshold">失敗が続いているとみなす連続失敗の回数 (<see cref="AppSettings.FailureThreshold"/>)。</param>
    public FailureAlert? Add(KeepAliveResult result, int threshold)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Success)
        {
            _consecutiveFailures++;
            _failingSince ??= result.Timestamp;
            if (IsFailing || _consecutiveFailures < Math.Max(1, threshold))
                return null;

            IsFailing = true;
            return new FailureAlert(FailureAlertKind.Failing, _consecutiveFailures, _failingSince.Value, null);
        }

        var wasFailing = IsFailing;
        var failures = _consecutiveFailures;
        var since = _failingSince;
        Reset();
        if (!wasFailing || since is null)
            return null;

        var duration = result.Timestamp - since.Value;
        return new FailureAlert(
            FailureAlertKind.Recovered, failures, since.Value, duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
    }

    /// <summary>
    /// 状態を初期に戻す。キープアライブを停止したときに呼ぶ
    /// (停止中は通信しないため、次に開始したときの成功を「復旧」として知らせない)。
    /// </summary>
    public void Reset()
    {
        _consecutiveFailures = 0;
        _failingSince = null;
        IsFailing = false;
    }
}
