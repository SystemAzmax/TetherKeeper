using System.Globalization;

namespace KeepAlive.Core.Resources;

/// <summary>
/// リソースの文言 ({0} などの埋め込み先を含む) に値を埋め込む。
/// 文言の言語は表示言語 (<see cref="CultureInfo.CurrentUICulture"/>)、数値や日時の書式は <see cref="CultureInfo.CurrentCulture"/> に従う。
/// </summary>
public static class Text
{
    public static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);
}
