namespace KeepAlive.Core.Interfaces;

/// <summary>
/// OS ログイン時の自動起動 (OS 依存。実装は Infrastructure / プラットフォーム側)。
/// MSIX 版の StartupTask API が非同期のため、非同期のインターフェースにしている。
/// </summary>
public interface IAutoStartService
{
    /// <summary>このプラットフォームで自動起動を設定できるか。</summary>
    bool IsSupported { get; }

    Task<bool> IsEnabledAsync();

    /// <summary>
    /// 自動起動を有効／無効にする。
    /// ユーザーがタスクマネージャー等で無効にしている、ポリシーで禁止されている等で変更できない場合は
    /// 理由をメッセージに含めた <see cref="InvalidOperationException"/> を送出する。
    /// </summary>
    Task SetEnabledAsync(bool enabled);
}
