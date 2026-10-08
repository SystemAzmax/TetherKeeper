using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// アプリ設定の取得・更新。保存先は <see cref="ISettingsStore"/> に委譲する。
/// </summary>
public interface ISettingsService
{
    /// <summary>現在の設定。<see cref="LoadAsync"/> 前は既定値。</summary>
    AppSettings Current { get; }

    event EventHandler<AppSettings>? SettingsChanged;

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定を検証して保存する。検証エラーがある場合は保存せずエラー一覧を返す。
    /// </summary>
    Task<IReadOnlyList<string>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
