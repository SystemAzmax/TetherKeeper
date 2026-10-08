using KeepAlive.Core.Interfaces;

namespace KeepAlive.Infrastructure.Power;

/// <summary>
/// 電源イベントを取得できないプラットフォーム用。
/// この場合も KeepAliveWorker の経過時間チェックで復帰を検出できる。
/// </summary>
public sealed class NullPowerEventSource : IPowerEventSource
{
#pragma warning disable CS0067 // 発火しないイベント
    public event EventHandler? Suspending;

    public event EventHandler? Resumed;
#pragma warning restore CS0067
}
