using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Infrastructure.Network;

/// <summary>
/// インターネットへの通信に使われているネットワークアダプターを調べ、OS が数えている送受信量を読む。
/// アダプターは、インターネット上のアドレスへ送るときに使われる自分のアドレスから判定する
/// (有線 LAN とテザリングの Wi-Fi の両方につながっている場合は、OS が通信に選んだ方になる)。
/// </summary>
public sealed class NetworkInterfaceDataUsageMeter : IDataUsageMeter
{
    // 経路を調べるだけで通信はしないため、インターネット上のアドレスであればどこでもよい。
    // IPv4 の経路が無い環境に備えて IPv6 も試す
    private static readonly IPAddress[] ProbeAddresses =
    [
        IPAddress.Parse("1.1.1.1"),
        IPAddress.Parse("2606:4700:4700::1111"),
    ];

    public DataUsageReading? Read()
    {
        foreach (var probe in ProbeAddresses)
        {
            if (FindLocalAddress(probe) is not { } localAddress)
                continue;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up
                    || !adapter.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(localAddress)))
                    continue;

                var statistics = adapter.GetIPStatistics();
                return new DataUsageReading(adapter.Id, adapter.Name, statistics.BytesReceived + statistics.BytesSent);
            }
        }

        return null;
    }

    /// <summary><paramref name="destination"/> へ送るときに使われる自分のアドレス。経路が無ければ null。</summary>
    private static IPAddress? FindLocalAddress(IPAddress destination)
    {
        try
        {
            // UDP の Connect は送信元のアドレスを決めるだけで、パケットは送らない (ポート番号は何でもよい)
            using var socket = new Socket(destination.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(destination, 53);
            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch (SocketException)
        {
            // ネットワークにつながっていない (経路が無い)
            return null;
        }
    }
}
