using System.Globalization;
using KeepAlive.Core.Models;
using KeepAlive.Core.Strategies;

namespace KeepAlive.Core.Services;

/// <summary>通信量の目安 (最小〜最大のバイト数)。</summary>
public readonly record struct TrafficRange(long MinBytes, long MaxBytes)
{
    public TrafficRange Multiply(double factor) =>
        new((long)Math.Round(MinBytes * factor), (long)Math.Round(MaxBytes * factor));

    /// <summary>
    /// 「約 14〜19 KB」「約 0.35〜0.46 MB」の形にする。単位は大きい方に合わせ、上限の設定と同じく 1 KB = 1,000 バイトとする。
    /// 上限の設定は MB で入れるため、0.1 MB 以上は MB で表す (「346 KB」より「0.35 MB」の方が比べやすい)。
    /// </summary>
    public string Format()
    {
        var (divisor, unit) = MaxBytes switch
        {
            < AppSettings.BytesPerMegabyte / 10 => (1_000.0, "KB"),
            < 100 * AppSettings.BytesPerMegabyte => ((double)AppSettings.BytesPerMegabyte, "MB"),
            _ => (1_000.0 * AppSettings.BytesPerMegabyte, "GB"),
        };
        var min = FormatNumber(MinBytes / divisor);
        var max = FormatNumber(MaxBytes / divisor);
        return min == max
            ? Text.Format(CoreStrings.Traffic_Single, max, unit)
            : Text.Format(CoreStrings.Traffic_Range, min, max, unit);
    }

    // 有効数字 2 桁ほどにそろえる (目安のため、細かい桁は出さない)
    private static string FormatNumber(double value) => value switch
    {
        < 1 => value.ToString("0.00", CultureInfo.InvariantCulture),
        < 10 => value.ToString("0.0", CultureInfo.InvariantCulture),
        _ => value.ToString("0", CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// 通信方式と実行間隔から、キープアライブ自身の通信量の目安を計算する。
/// 値は IPv4 と IPv6 の差などを含めた概算 (<c>docs/設計判断.md</c> の「2. 通信方式の比較」を参照)。
/// ほかのアプリの通信、名前解決 (DNS)、失敗したときの予備の通信方式の分は含まない。
/// </summary>
public static class TrafficEstimate
{
    /// <summary>Ping 1 回 (要求と応答、32 バイトのデータ) の通信量。IPv4 で約 120 バイト、IPv6 で約 160 バイト。</summary>
    private static readonly TrafficRange PingPerRun = new(120, 160);

    /// <summary>TCP の接続と切断 1 回 (SYN〜FIN の 6〜8 パケット) の通信量。</summary>
    private static readonly TrafficRange TcpPerConnection = new(400, 600);

    /// <summary>接続を使い回したときの HTTP HEAD 1 回 (要求と応答のヘッダー) の通信量。</summary>
    private static readonly TrafficRange HttpPerRequest = new(500, 1_000);

    /// <summary>
    /// HTTPS で新しく接続したときに TLS のハンドシェイクで増える通信量。
    /// セッションを再開できれば小さく、証明書を受け取ると数 KB になる。
    /// </summary>
    private static readonly TrafficRange TlsPerConnection = new(1_000, 7_000);

    /// <summary>1 時間あたりの通信量の目安。設定の値が範囲外 (間隔が 0 以下など) なら null。</summary>
    public static TrafficRange? PerHour(KeepAliveMethod method, int intervalSeconds, string? httpUrl = null)
    {
        if (intervalSeconds <= 0 || !Enum.IsDefined(method))
            return null;

        var runsPerHour = 3600.0 / intervalSeconds;
        return method switch
        {
            KeepAliveMethod.Ping => PingPerRun.Multiply(runsPerHour),
            KeepAliveMethod.TcpConnect => TcpPerConnection.Multiply(runsPerHour),
            _ => HttpPerHour(runsPerHour, intervalSeconds, httpUrl),
        };
    }

    /// <summary>設定の通信方式と実行間隔での、1 時間あたりの通信量の目安。</summary>
    public static TrafficRange? PerHour(AppSettings settings) =>
        PerHour(settings.Method, settings.IntervalSeconds, settings.HttpUrl);

    /// <summary>
    /// HTTP HEAD は接続を使い回す。間隔が接続を閉じるまでの時間より短ければ、接続の作り直しは
    /// 接続を使い続ける最長の時間 (<see cref="HttpHeadKeepAliveStrategy.ConnectionLifetime"/>) ごと、長ければ毎回になる。
    /// </summary>
    private static TrafficRange HttpPerHour(double runsPerHour, int intervalSeconds, string? httpUrl)
    {
        var connectionsPerHour = intervalSeconds > HttpHeadKeepAliveStrategy.ConnectionIdleTimeout.TotalSeconds
            ? runsPerHour
            : Math.Min(runsPerHour, TimeSpan.FromHours(1) / HttpHeadKeepAliveStrategy.ConnectionLifetime);

        var isHttps = Uri.TryCreate(httpUrl?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        var perConnection = isHttps
            ? new TrafficRange(
                TcpPerConnection.MinBytes + TlsPerConnection.MinBytes,
                TcpPerConnection.MaxBytes + TlsPerConnection.MaxBytes)
            : TcpPerConnection;

        var requests = HttpPerRequest.Multiply(runsPerHour);
        var connections = perConnection.Multiply(connectionsPerHour);
        return new TrafficRange(requests.MinBytes + connections.MinBytes, requests.MaxBytes + connections.MaxBytes);
    }
}
