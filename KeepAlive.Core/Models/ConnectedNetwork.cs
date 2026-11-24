namespace KeepAlive.Core.Models;

/// <summary>ネットワークの種類。</summary>
public enum NetworkKind
{
    /// <summary>Wi-Fi。</summary>
    WiFi,

    /// <summary>モバイル回線 (SIM)。</summary>
    Mobile,

    /// <summary>有線 LAN、USB や Bluetooth のテザリングなど。</summary>
    Other,
}

/// <summary>接続中のネットワーク。</summary>
/// <param name="Name">
/// ネットワークの名前。Wi-Fi はネットワーク名 (SSID)、それ以外は OS が付けた名前 (「イーサネット 2」など)。
/// </param>
/// <param name="Kind">ネットワークの種類。</param>
public sealed record ConnectedNetwork(string Name, NetworkKind Kind)
{
    /// <summary>一覧に表示する文字列 (例: 「iPhone（Wi-Fi）」)。</summary>
    public string DisplayName => Kind switch
    {
        NetworkKind.WiFi => $"{Name}（Wi-Fi）",
        NetworkKind.Mobile => $"{Name}（モバイル回線）",
        _ => Name,
    };
}
