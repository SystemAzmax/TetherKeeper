using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 通信量の累計の永続化先 (OS 依存。実装は Infrastructure)。
/// </summary>
public interface IDataUsageStore
{
    /// <summary>保存済みの累計を読み込む。未保存の場合は null。</summary>
    Task<DataUsage?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DataUsage usage, CancellationToken cancellationToken = default);
}
