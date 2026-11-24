using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using Windows.Networking.Connectivity;

namespace TetherKeeper.WinUI;

/// <summary>
/// WinRT の <see cref="NetworkInformation"/> で、接続中のネットワークを取得する。
/// </summary>
/// <remarks>
/// <para>
/// 名前には接続プロファイルの名前 (<see cref="ConnectionProfile.ProfileName"/>) を使う。
/// Wi-Fi ではネットワーク名 (SSID)、それ以外 (有線 LAN、USB や Bluetooth のテザリング) では、
/// Windows の「ネットワークとインターネット」に表示される名前になる。
/// </para>
/// <para>
/// Windows 11 24H2 から、Wi-Fi の SSID を返す WLAN API (WlanQueryInterface など) や Windows.Devices.WiFi は、
/// 位置情報の許可が無いと使えなくなった。接続プロファイルの名前は、その制限の対象外のため、許可を求めずに使える。
/// </para>
/// </remarks>
internal sealed class WindowsNetworkMonitor : INetworkMonitor, IDisposable
{
    public WindowsNetworkMonitor()
    {
        // NetworkStatusChanged だけでは取りこぼすことがあるため、IP アドレスの変化も見る
        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public bool IsSupported => true;

    public event EventHandler? NetworksChanged;

    public IReadOnlyList<ConnectedNetwork> GetConnectedNetworks()
    {
        var networks = new List<ConnectedNetwork>();
        foreach (var profile in NetworkInformation.GetConnectionProfiles())
        {
            // 一覧には過去に接続したネットワークも含まれるため、今つながっているものだけにする
            if (string.IsNullOrEmpty(profile.ProfileName)
                || profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None
                || networks.Exists(n => n.Name == profile.ProfileName))
                continue;

            var kind = profile.IsWlanConnectionProfile ? NetworkKind.WiFi
                : profile.IsWwanConnectionProfile ? NetworkKind.Mobile
                : NetworkKind.Other;
            networks.Add(new ConnectedNetwork(profile.ProfileName, kind));
        }

        return networks;
    }

    public void Dispose()
    {
        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
    }

    private void OnNetworkStatusChanged(object sender) => RaiseNetworksChanged();

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => RaiseNetworksChanged();

    private void RaiseNetworksChanged()
    {
        try
        {
            NetworksChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // OS からのコールバックで例外を外に出すと、アプリが落ちることがある
            System.Diagnostics.Debug.WriteLine($"WindowsNetworkMonitor: {ex}");
        }
    }
}
