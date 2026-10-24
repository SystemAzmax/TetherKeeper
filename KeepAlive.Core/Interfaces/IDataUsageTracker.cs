using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// キープアライブ動作中の通信量を計測し、リセットするまで累計する。累計はアプリの再起動をまたいで保存する。
/// 計測するのはインターネットへの通信に使われているネットワークアダプターの送受信量で、ほかのアプリの通信も含む。
/// </summary>
public interface IDataUsageTracker
{
    /// <summary>現在の累計。<see cref="LoadAsync"/> 前は、それまでに計測した分だけ。</summary>
    DataUsage Current { get; }

    /// <summary>累計が変わった。バックグラウンドスレッドから発火する。</summary>
    event EventHandler<DataUsage>? Changed;

    /// <summary>保存済みの累計を読み込む。読み込む前に計測した分は、読み込んだ累計に加える。</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>累計を 0 に戻して保存する。</summary>
    Task ResetAsync();

    /// <summary>
    /// 計測を始める (今の送受信量を基準にする)。計測は戻り値の <see cref="IDataUsageMeasurement.SampleAsync"/> で行い、
    /// 破棄すると最後の計測と保存を行って終わる。
    /// </summary>
    IDataUsageMeasurement StartMeasurement();
}
