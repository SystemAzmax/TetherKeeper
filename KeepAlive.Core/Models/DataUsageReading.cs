namespace KeepAlive.Core.Models;

/// <summary>
/// ネットワークアダプターの送受信量の読み取り値 (OS が数えている累計値)。
/// </summary>
/// <param name="InterfaceId">アダプターの識別子。読み取り値どうしの差を取ってよいか (同じアダプターか) の判定に使う。</param>
/// <param name="InterfaceName">表示用のアダプター名 (例: Wi-Fi)。</param>
/// <param name="TotalBytes">送受信の合計 (バイト)。</param>
public sealed record DataUsageReading(string InterfaceId, string InterfaceName, long TotalBytes);
