using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 設定の永続化先 (OS 依存。実装は Infrastructure)。
/// </summary>
public interface ISettingsStore
{
    /// <summary>保存済み設定を読み込む。未保存の場合は null。</summary>
    Task<AppSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
