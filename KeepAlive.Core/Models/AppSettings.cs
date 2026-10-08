namespace KeepAlive.Core.Models;

/// <summary>
/// アプリケーション設定。永続化対象のため不変 record として扱い、変更は <c>with</c> 式で行う。
/// </summary>
public sealed record AppSettings
{
    public const int MinIntervalSeconds = 5;
    public const int MaxIntervalSeconds = 3600;
    public const int MinTimeoutMilliseconds = 500;
    public const int MaxTimeoutMilliseconds = 30_000;

    /// <summary>使用する通信方式。</summary>
    public KeepAliveMethod Method { get; init; } = KeepAliveMethod.Ping;

    /// <summary>実行間隔 (秒)。</summary>
    public int IntervalSeconds { get; init; } = 30;

    /// <summary>1 回の通信のタイムアウト (ミリ秒)。</summary>
    public int TimeoutMilliseconds { get; init; } = 3000;

    /// <summary>Ping の送信先 (ホスト名または IP アドレス)。</summary>
    public string PingHost { get; init; } = "1.1.1.1";

    /// <summary>HTTP HEAD の送信先 URL。</summary>
    public string HttpUrl { get; init; } = "http://www.msftconnecttest.com/connecttest.txt";

    /// <summary>TCP 接続先ホスト。</summary>
    public string TcpHost { get; init; } = "1.1.1.1";

    /// <summary>TCP 接続先ポート。</summary>
    public int TcpPort { get; init; } = 443;

    /// <summary>アプリ起動時にキープアライブを自動開始するか。</summary>
    public bool StartOnLaunch { get; init; }

    /// <summary>OS ログイン時にアプリを自動起動するか (対応 OS のみ)。</summary>
    public bool LaunchAtLogin { get; init; }

    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

    public TimeSpan Timeout => TimeSpan.FromMilliseconds(TimeoutMilliseconds);

    /// <summary>
    /// 入力値の検証エラーを返す。エラーが無ければ空。
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!Enum.IsDefined(Method))
            errors.Add("通信方式が不正です。");
        if (IntervalSeconds is < MinIntervalSeconds or > MaxIntervalSeconds)
            errors.Add($"実行間隔は {MinIntervalSeconds}〜{MaxIntervalSeconds} 秒で指定してください。");
        if (TimeoutMilliseconds is < MinTimeoutMilliseconds or > MaxTimeoutMilliseconds)
            errors.Add($"タイムアウトは {MinTimeoutMilliseconds}〜{MaxTimeoutMilliseconds} ミリ秒で指定してください。");
        if (TimeoutMilliseconds >= IntervalSeconds * 1000L)
            errors.Add("タイムアウトは実行間隔より短くしてください。");

        switch (Method)
        {
            case KeepAliveMethod.Ping when string.IsNullOrWhiteSpace(PingHost):
                errors.Add("Ping の送信先を入力してください。");
                break;
            case KeepAliveMethod.HttpHead when !IsValidHttpUrl(HttpUrl):
                errors.Add("HTTP の URL は http:// または https:// で始まる絶対 URL を指定してください。");
                break;
            case KeepAliveMethod.TcpConnect when string.IsNullOrWhiteSpace(TcpHost):
                errors.Add("TCP の接続先ホストを入力してください。");
                break;
            case KeepAliveMethod.TcpConnect when TcpPort is < 1 or > 65535:
                errors.Add("TCP のポートは 1〜65535 で指定してください。");
                break;
        }

        return errors;
    }

    /// <summary>
    /// 範囲外の値を許容範囲に丸めた設定を返す。設定ファイルの手編集・破損に備えて読み込み時に適用する。
    /// </summary>
    public AppSettings Normalize()
    {
        var defaults = new AppSettings();
        var interval = Math.Clamp(IntervalSeconds, MinIntervalSeconds, MaxIntervalSeconds);
        var timeout = Math.Clamp(TimeoutMilliseconds, MinTimeoutMilliseconds, MaxTimeoutMilliseconds);
        if (timeout >= interval * 1000)
            timeout = Math.Max(MinTimeoutMilliseconds, interval * 1000 / 2);

        return this with
        {
            Method = Enum.IsDefined(Method) ? Method : defaults.Method,
            IntervalSeconds = interval,
            TimeoutMilliseconds = timeout,
            PingHost = string.IsNullOrWhiteSpace(PingHost) ? defaults.PingHost : PingHost.Trim(),
            HttpUrl = IsValidHttpUrl(HttpUrl) ? HttpUrl.Trim() : defaults.HttpUrl,
            TcpHost = string.IsNullOrWhiteSpace(TcpHost) ? defaults.TcpHost : TcpHost.Trim(),
            TcpPort = TcpPort is >= 1 and <= 65535 ? TcpPort : defaults.TcpPort,
        };
    }

    private static bool IsValidHttpUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
