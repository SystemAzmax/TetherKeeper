using System.Globalization;

namespace KeepAlive.Core.Services;

/// <summary>
/// 「このアプリについて」に表示する文と、使い方ガイドの URL を作成するクラスです。
/// </summary>
public static class AppAboutInfo
{
    /// <summary>日本語の使い方ガイドのファイル名。</summary>
    private const string USER_GUIDE_JA_FILE_NAME = "UserGuide_ja.md";

    /// <summary>英語の使い方ガイドのファイル名。日本語以外の言語では、こちらを開きます。</summary>
    private const string USER_GUIDE_EN_FILE_NAME = "UserGuide.md";

    /// <summary>
    /// 表示言語に合わせて、使い方ガイドの URL を作成します。
    /// 日本語の場合は日本語版、それ以外の言語の場合は英語版のガイドを返します。
    /// </summary>
    /// <param name="baseUrl">ガイドのあるフォルダーの URL（例: https://github.com/SystemAzmax/AppRoulette/blob/main/docs）。</param>
    /// <param name="culture">表示言語。</param>
    /// <returns>使い方ガイドの URL。</returns>
    public static string CreateUserGuideUrl(string baseUrl, CultureInfo culture)
    {
        var fileName = culture.TwoLetterISOLanguageName == "ja"
            ? USER_GUIDE_JA_FILE_NAME
            : USER_GUIDE_EN_FILE_NAME;
        return $"{baseUrl.TrimEnd('/')}/{fileName}";
    }

    /// <summary>
    /// バージョンを表示用の文字列にします。
    /// ストアに出すパッケージは 4 つ目の数字（リビジョン）が 0 のため、0 の場合は省きます。
    /// </summary>
    /// <param name="version">表示するバージョン。</param>
    /// <returns>「1.0.0」や「1.0.0.1」の形のバージョン。</returns>
    public static string FormatVersion(Version version)
    {
        var build = Math.Max(version.Build, 0);
        return version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{build}.{version.Revision}"
            : $"{version.Major}.{version.Minor}.{build}";
    }

    /// <summary>
    /// 「このアプリについて」の本文を作成します。
    /// </summary>
    /// <param name="productName">アプリ名。</param>
    /// <param name="version">アプリのバージョン。</param>
    /// <param name="isPackaged">ストア版（パッケージあり）の場合は <c>true</c>、exe 版の場合は <c>false</c>。</param>
    /// <param name="copyright">著作権表記。無い場合は null。</param>
    /// <returns>アプリ名・バージョン・著作権表記を含む本文。</returns>
    public static string CreateMessage(
        string productName,
        Version version,
        bool isPackaged,
        string? copyright)
    {
        var edition = isPackaged ? "ストア版" : "exe 版";
        var message = $"{productName}\nバージョン {FormatVersion(version)}（{edition}）";
        return string.IsNullOrWhiteSpace(copyright)
            ? message
            : $"{message}\n\n{copyright}";
    }
}
