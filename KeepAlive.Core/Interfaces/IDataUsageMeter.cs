using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// ネットワークアダプターの送受信量の読み取り (OS 依存。実装は Infrastructure)。
/// </summary>
public interface IDataUsageMeter
{
    /// <summary>
    /// インターネットへの通信に使われているアダプターの送受信量を返す。該当するアダプターが無ければ null。
    /// </summary>
    DataUsageReading? Read();
}
