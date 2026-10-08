using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 設定間隔でキープアライブ通信を繰り返すバックグラウンド処理。
/// </summary>
public interface IKeepAliveWorker
{
    bool IsRunning { get; }

    KeepAliveStatistics Statistics { get; }

    /// <summary>次回の実行予定時刻。停止中は null。</summary>
    DateTimeOffset? NextRunAt { get; }

    /// <summary>実行状態 (<see cref="IsRunning"/>) が変化した。</summary>
    event EventHandler? StateChanged;

    /// <summary>通信が 1 回完了した。バックグラウンドスレッドから発火する。</summary>
    event EventHandler<KeepAliveResult>? ResultReceived;

    void Start();

    Task StopAsync();

    /// <summary>待機中であれば次回実行を前倒しする。</summary>
    void TriggerNow();

    void ResetStatistics();
}
