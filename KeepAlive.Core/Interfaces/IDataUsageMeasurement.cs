namespace KeepAlive.Core.Interfaces;

/// <summary>
/// <see cref="IDataUsageTracker.StartMeasurement"/> で始めた 1 回分の計測 (キープアライブの開始から停止まで)。
/// メソッドを同時に呼ばないこと。
/// </summary>
public interface IDataUsageMeasurement : IAsyncDisposable
{
    /// <summary>前回の計測からの増加分を累計に加える。</summary>
    Task SampleAsync();
}
