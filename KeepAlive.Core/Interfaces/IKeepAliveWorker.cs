using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 設定間隔でキープアライブ通信を繰り返すバックグラウンド処理。
/// <see cref="AppSettings.AutoStopAt"/> が設定されていれば、その日時に自動で停止する。
/// <see cref="AppSettings.DataLimitMegabytes"/> が設定されていれば、通信量の累計 (<see cref="IDataUsageTracker"/>) が
/// 上限に達したときに自動で停止し、達している間は <see cref="Start"/> しても開始しない。
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
