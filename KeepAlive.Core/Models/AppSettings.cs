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
    public const int MaxDataLimitMegabytes = 1_000_000;
    public const int MinFailureThreshold = 1;
    public const int MaxFailureThreshold = 100;

    /// <summary>
    /// 通信量の 1 MB のバイト数。上限が少なめ (早めに止まる側) になるよう、1 MB = 1,000,000 バイトとする。
    /// </summary>
    public const long BytesPerMegabyte = 1_000_000;

    /// <summary>使用する通信方式。</summary>
    public KeepAliveMethod Method { get; init; } = KeepAliveMethod.Ping;

    /// <summary>
    /// 予備の通信方式。<see cref="Method"/> で失敗したときだけ、この方式 (送信先はこの方式の設定) で 1 回やり直し、
    /// どちらも失敗したときだけ失敗とする。null は予備を使わない。
    /// 送信先の障害や ICMP の遮断などで 1 つの方式だけが失敗する場合と、回線そのものの切断を区別するため。
    /// </summary>
    public KeepAliveMethod? FallbackMethod { get; init; }

    /// <summary>実行間隔 (秒)。</summary>
    public int IntervalSeconds { get; init; } = 30;

    /// <summary>1 回の通信のタイムアウト (ミリ秒)。</summary>
    public int TimeoutMilliseconds { get; init; } = 3000;

    /// <summary>Ping の送信先 (ホスト名または IP アドレス)。</summary>
    public string PingHost { get; init; } = "1.1.1.1";

    /// <summary>
    /// HTTP HEAD の送信先 URL。
    /// 既定値を平文 HTTP にしているのは、HTTPS は TLS ハンドシェイクで 1 回あたりの通信量が数 KB 増え、
    /// テザリングの通信量を圧迫するため。送受信するのは固定の疎通確認用ファイルのヘッダーだけで、機密情報は含まない。
    /// </summary>
    public string HttpUrl { get; init; } = "http://www.msftconnecttest.com/connecttest.txt";

    /// <summary>TCP 接続先ホスト。</summary>
    public string TcpHost { get; init; } = "1.1.1.1";

    /// <summary>TCP 接続先ポート。</summary>
    public int TcpPort { get; init; } = 443;

    /// <summary>アプリ起動時にキープアライブを自動開始するか。</summary>
    public bool StartOnLaunch { get; init; }

    /// <summary>OS ログイン時にアプリを自動起動するか (対応 OS のみ)。</summary>
    public bool LaunchAtLogin { get; init; }

    /// <summary>
    /// キープアライブを自動停止する日時。null は無効。
    /// 動作中にこの日時を迎えると停止し、値は解除 (null) される。
    /// </summary>
    public DateTimeOffset? AutoStopAt { get; init; }

    /// <summary>
    /// 毎週決まった曜日 (<see cref="WeeklyStopDays"/>) の決まった時刻 (<see cref="WeeklyStopTime"/>) に、キープアライブを停止するか。
    /// <see cref="AutoStopAt"/> と違い、停止しても解除しない。
    /// </summary>
    public bool WeeklyStopEnabled { get; init; }

    /// <summary>毎週の自動停止の時刻 (PC の時刻)。</summary>
    public TimeOnly WeeklyStopTime { get; init; } = new(18, 0);

    /// <summary>毎週の自動停止の曜日。</summary>
    public WeeklyDays WeeklyStopDays { get; init; } = WeeklyDays.Everyday;

    /// <summary>
    /// 日時の予約 (<see cref="AutoStopAt"/>) か毎週の自動停止で停止したときに、アプリも終了するか。
    /// 通信量の上限やネットワークの切断で停止したときは、理由を確かめられるよう終了しない。
    /// </summary>
    public bool ExitAppOnAutoStop { get; init; }

    /// <summary>
    /// 通信量の上限 (MB)。0 は無効。
    /// 通信量の累計 (<see cref="Interfaces.IDataUsageTracker"/>) がこの値に達するとキープアライブを停止し、
    /// 累計をリセットするまでは開始できなくなる。自動停止日時と違い、停止しても値は解除しない。
    /// </summary>
    public int DataLimitMegabytes { get; init; }

    /// <summary>
    /// 通信の失敗が続いたとき (<see cref="FailureThreshold"/> 回連続) と、そこから復旧したときに通知するか (対応 OS のみ)。
    /// </summary>
    public bool NotifyOnFailure { get; init; } = true;

    /// <summary>
    /// この回数だけ続けて失敗したら、接続が切れているとみなして通知し、トレイアイコンに印を付ける。
    /// 1 回の失敗は一時的なことがあるため、既定は 3 回にする。
    /// </summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>
    /// キープアライブの動作中は、操作しないで放置したときの自動スリープをしないようにするか (対応 OS のみ)。
    /// スリープ中は通信できず、その間に切断されるのを防ぐため。電源の設定を尊重し、既定はオフにする。
    /// 画面の消灯は妨げない。
    /// </summary>
    public bool PreventSleepWhileRunning { get; init; }

    /// <summary>
    /// <see cref="AutoControlNetworkName"/> のネットワークに接続したらキープアライブを開始し、切断したら停止するか (対応 OS のみ)。
    /// </summary>
    public bool AutoControlByNetwork { get; init; }

    /// <summary>
    /// 自動で開始・停止するきっかけにするネットワークの名前 (Wi-Fi はネットワーク名 (SSID))。大文字と小文字は区別する。
    /// </summary>
    public string AutoControlNetworkName { get; init; } = string.Empty;

    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

    /// <summary>通信量の上限 (バイト)。無効なら null。</summary>
    public long? DataLimitBytes => DataLimitMegabytes > 0 ? DataLimitMegabytes * BytesPerMegabyte : null;

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
        if (DataLimitMegabytes is < 0 or > MaxDataLimitMegabytes)
            errors.Add($"通信量の上限は 0〜{MaxDataLimitMegabytes} MB で指定してください。");
        if (FailureThreshold is < MinFailureThreshold or > MaxFailureThreshold)
            errors.Add($"通知する連続失敗の回数は {MinFailureThreshold}〜{MaxFailureThreshold} 回で指定してください。");
        if (WeeklyStopEnabled && (WeeklyStopDays & WeeklyDays.Everyday) == WeeklyDays.None)
            errors.Add("毎週の自動停止の曜日を 1 つ以上選んでください。");
        if (AutoControlByNetwork && string.IsNullOrWhiteSpace(AutoControlNetworkName))
            errors.Add("自動で開始・停止するネットワークを選んでください。");

        // 送信先は、使う方式 (通信方式と予備の通信方式) の分だけ確かめる
        ValidateDestination(Method, errors);
        if (FallbackMethod is { } fallback)
        {
            if (!Enum.IsDefined(fallback))
                errors.Add("予備の通信方式が不正です。");
            else if (fallback == Method)
                errors.Add("予備の通信方式には、通信方式と別の方式を選んでください。");
            else
                ValidateDestination(fallback, errors);
        }

        return errors;
    }

    private void ValidateDestination(KeepAliveMethod method, List<string> errors)
    {
        switch (method)
        {
            case KeepAliveMethod.Ping when string.IsNullOrWhiteSpace(PingHost):
                errors.Add("Ping の送信先を入力してください。");
                break;
            case KeepAliveMethod.HttpHead when !IsValidHttpUrl(HttpUrl):
                errors.Add("HTTP の URL は http:// または https:// で始まる絶対 URL を指定してください。");
                break;
            case KeepAliveMethod.HttpHead when HasUserInfo(HttpUrl):
                // 疎通確認に認証は不要。含めると設定ファイル・ログ・画面に平文で残ってしまう
                errors.Add("HTTP の URL にユーザー名やパスワードを含めないでください。");
                break;
            case KeepAliveMethod.TcpConnect when string.IsNullOrWhiteSpace(TcpHost):
                errors.Add("TCP の接続先ホストを入力してください。");
                break;
            case KeepAliveMethod.TcpConnect when TcpPort is < 1 or > 65535:
                errors.Add("TCP のポートは 1〜65535 で指定してください。");
                break;
        }
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

        var method = Enum.IsDefined(Method) ? Method : defaults.Method;
        return this with
        {
            Method = method,
            // 不正な値や、通信方式と同じ方式は、予備なしにする
            FallbackMethod = FallbackMethod is { } fallback && Enum.IsDefined(fallback) && fallback != method ? fallback : null,
            IntervalSeconds = interval,
            TimeoutMilliseconds = timeout,
            PingHost = string.IsNullOrWhiteSpace(PingHost) ? defaults.PingHost : PingHost.Trim(),
            HttpUrl = IsValidHttpUrl(HttpUrl) ? RemoveUserInfo(HttpUrl.Trim()) : defaults.HttpUrl,
            TcpHost = string.IsNullOrWhiteSpace(TcpHost) ? defaults.TcpHost : TcpHost.Trim(),
            TcpPort = TcpPort is >= 1 and <= 65535 ? TcpPort : defaults.TcpPort,
            DataLimitMegabytes = Math.Clamp(DataLimitMegabytes, 0, MaxDataLimitMegabytes),
            FailureThreshold = Math.Clamp(FailureThreshold, MinFailureThreshold, MaxFailureThreshold),
            // 秒以下は画面で扱わないため切り捨て、曜日が無い場合は無効にする
            WeeklyStopTime = new TimeOnly(WeeklyStopTime.Hour, WeeklyStopTime.Minute),
            WeeklyStopDays = WeeklyStopDays & WeeklyDays.Everyday,
            WeeklyStopEnabled = WeeklyStopEnabled && (WeeklyStopDays & WeeklyDays.Everyday) != WeeklyDays.None,
            // 名前が無いと判定できないため、手編集などで空になっていたら無効にする
            AutoControlNetworkName = AutoControlNetworkName?.Trim() ?? string.Empty,
            AutoControlByNetwork = AutoControlByNetwork && !string.IsNullOrWhiteSpace(AutoControlNetworkName),
        };
    }

    private static bool IsValidHttpUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool HasUserInfo(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.UserInfo.Length > 0;

    /// <summary>
    /// URL からユーザー名・パスワードを取り除く。手編集された設定ファイルなどに含まれていた場合に、
    /// ログや画面へ平文で出さないため。含まれていなければ表記を変えないよう元の文字列をそのまま返す。
    /// </summary>
    private static string RemoveUserInfo(string url)
    {
        if (!HasUserInfo(url))
            return url;

        var builder = new UriBuilder(url) { UserName = string.Empty, Password = string.Empty };
        return builder.Uri.AbsoluteUri;
    }
}
