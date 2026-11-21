using System.Globalization;

namespace KeepAlive.Core.Services;

/// <summary>
/// 画面に表示するアプリ名を、表示言語に合わせて選ぶクラスです。
/// 日本語では日本語名、それ以外の言語では英語名を使います。
/// </summary>
/// <remarks>
/// スタートメニューなどに出る MSIX 版の名前は、パッケージのリソース
/// （TetherKeeper\Platforms\Windows\Strings フォルダーの Resources.resw）で同じように切り替えます。
/// 名前を変えるときは、両方を合わせて直します。
/// データの保存先などの識別子には、表示名ではなく英語名の固定の文字列を使います。
/// </remarks>
public static class AppDisplayName
{
    /// <summary>日本語のアプリ名。</summary>
    public const string JAPANESE = "テザリングキーパー";

    /// <summary>日本語以外の言語で使う、英語のアプリ名。</summary>
    public const string ENGLISH = "TetherKeeper";

    /// <summary>
    /// 現在の表示言語に合ったアプリ名を取得します。
    /// </summary>
    /// <returns>アプリ名。</returns>
    public static string GetCurrent() => Get(CultureInfo.CurrentUICulture);

    /// <summary>
    /// 指定した言語に合ったアプリ名を取得します。
    /// </summary>
    /// <param name="culture">表示言語。</param>
    /// <returns>日本語の場合は日本語名、それ以外の場合は英語名。</returns>
    public static string Get(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "ja" ? JAPANESE : ENGLISH;
}
