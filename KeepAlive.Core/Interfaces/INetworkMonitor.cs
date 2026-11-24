using KeepAlive.Core.Models;

namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 接続中のネットワークの取得と、接続の変化の通知 (OS 依存。実装は Infrastructure または UI 側のプラットフォーム別のコード)。
/// </summary>
public interface INetworkMonitor
{
    /// <summary>この OS でネットワークを取得できるか。</summary>
    bool IsSupported { get; }

    /// <summary>接続の状態が変わった可能性がある。1 回の変化で何度も発火することがある。どのスレッドから発火してもよい。</summary>
    event EventHandler? NetworksChanged;

    /// <summary>今つながっているネットワークを返す。</summary>
    IReadOnlyList<ConnectedNetwork> GetConnectedNetworks();
}
