namespace KeepAlive.Core.Models;

/// <summary>
/// キープアライブ通信の方式。
/// </summary>
public enum KeepAliveMethod
{
    /// <summary>ICMP Echo (Ping)。</summary>
    Ping = 0,

    /// <summary>HTTP HEAD リクエスト。</summary>
    HttpHead = 1,

    /// <summary>TCP 接続確立のみ。</summary>
    TcpConnect = 2,
}
