using System.Globalization;

namespace KeepAlive.Core.Models;

/// <summary>アプリが用意している表示言語。</summary>
/// <param name="Code">言語のコード (例: en、ja)。設定 (<see cref="AppSettings.Language"/>) に保存する値。</param>
/// <param name="NativeName">
/// その言語自身で書いた言語名 (例: English、日本語)。今の表示言語が読めなくても選べるよう、訳さずに表示する。
/// </param>
public sealed record AppLanguage(string Code, string NativeName);

/// <summary>
/// アプリが用意している表示言語の一覧。
/// 言語を足すときは、CoreStrings と AppStrings の <c>.&lt;コード&gt;.resx</c> を作り、<see cref="Supported"/> に足す
/// (docs/BuildGuide.md の「画面の文言」を参照)。
/// </summary>
public static class AppLanguages
{
    /// <summary>既定の言語 (リソースの .resx に拡張子の前の言語が無いもの)。用意していない言語では、この言語になる。</summary>
    public const string DefaultCode = "en";

    /// <summary>用意している言語 (設定画面の一覧の順)。</summary>
    public static IReadOnlyList<AppLanguage> Supported { get; } =
    [
        new(DefaultCode, "English"),
        new("ja", "日本語"),
    ];

    /// <summary>用意している言語のコードか。</summary>
    public static bool IsSupported(string? code) =>
        !string.IsNullOrEmpty(code) && Supported.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 設定の言語のコードから、表示に使う言語を返す。空 (Windows の表示言語に合わせる) や、用意していない言語なら null。
    /// </summary>
    public static CultureInfo? Resolve(string? code) =>
        IsSupported(code) ? CultureInfo.GetCultureInfo(code!) : null;

    /// <summary>リソースを用意している言語 (既定の言語は <see cref="CultureInfo.InvariantCulture"/>)。</summary>
    public static IEnumerable<CultureInfo> ResourceCultures =>
        Supported.Select(l => l.Code == DefaultCode ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(l.Code));
}
