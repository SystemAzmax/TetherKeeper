using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Infrastructure.Network;

/// <summary>
/// 接続中のネットワークを取得できないプラットフォーム用。
/// Windows では、UI 側 (TetherKeeper の Platforms/Windows) で、WinRT の API を使う実装に差し替える。
/// </summary>
public sealed class UnsupportedNetworkMonitor : INetworkMonitor
{
    public bool IsSupported => false;

#pragma warning disable CS0067 // 発火しないイベント
    public event EventHandler? NetworksChanged;
#pragma warning restore CS0067

    public IReadOnlyList<ConnectedNetwork> GetConnectedNetworks() => [];
}
