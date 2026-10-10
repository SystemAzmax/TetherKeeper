using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// アプリ設定の取得・更新。保存先は <see cref="ISettingsStore"/> に委譲する。
/// </summary>
public interface ISettingsService
{
    /// <summary>現在の設定。<see cref="LoadAsync"/> 前は既定値。</summary>
    AppSettings Current { get; }

    /// <summary>
    /// 設定が変わった。保存の順序どおりに通知される。
    /// ハンドラ内から <see cref="SaveAsync"/> / <see cref="UpdateAsync"/> を同期的に待たないこと (デッドロックする)。
    /// </summary>
    event EventHandler<AppSettings>? SettingsChanged;

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定を検証して保存する。検証エラーがある場合は保存せずエラー一覧を返す。
    /// </summary>
    Task<IReadOnlyList<string>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存直前の最新の設定に <paramref name="update"/> を適用して保存する。
    /// 「現在の設定を読んで一部を書き換えて保存」を他の保存と競合せずに行うため、
    /// 一部の項目だけを変更する場合は <see cref="SaveAsync"/> ではなくこちらを使うこと。
    /// 変更が無ければ保存しない。
    /// </summary>
    Task<IReadOnlyList<string>> UpdateAsync(
        Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default);
}
