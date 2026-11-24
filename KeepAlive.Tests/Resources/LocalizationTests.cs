using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KeepAlive.Core.Models;
using KeepAlive.Core.Resources;
using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Resources;

public partial class LocalizationTests
{
    [GeneratedRegex(@"\{(\d+)(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private static Dictionary<string, string> Load(ResourceManager manager, CultureInfo culture)
    {
        // 親の言語にさかのぼらず、その言語のファイルにある項目だけを読む
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"{culture.Name} のリソースがありません。");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    private static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order());

    [Fact]
    public void CoreStrings_AllLanguages_HaveSameKeysAndPlaceholdersAsEnglish()
    {
        var english = Load(CoreStrings.ResourceManager, CultureInfo.InvariantCulture);

        // 言語を足したら、その言語も自動で確かめる (AppLanguages.Supported)
        foreach (var culture in AppLanguages.ResourceCultures.Where(c => !Equals(c, CultureInfo.InvariantCulture)))
        {
            var translated = Load(CoreStrings.ResourceManager, culture);
            Assert.Equal(english.Keys.Order(), translated.Keys.Order());
            foreach (var (key, text) in english)
            {
                Assert.False(string.IsNullOrWhiteSpace(text), key);
                // 埋め込む値の番号がずれていると、言語によって表示が崩れたり例外になったりする
                Assert.True(Placeholders(text) == Placeholders(translated[key]), $"{culture.Name} {key}: {text} / {translated[key]}");
            }
        }
    }

    /// <summary>
    /// 画面の文言 (Windows 版の AppStrings と macOS 版の MacStrings)。
    /// どちらもテストから参照できないプロジェクトにあるため、.resx のファイルを直接読んで確かめる。
    /// </summary>
    [Theory]
    [InlineData("TetherKeeper/Resources/Strings/AppStrings")]
    [InlineData("TetherKeeper.Mac/Strings/MacStrings")]
    public void UiStrings_AllLanguages_HaveSameKeysAndPlaceholdersAsEnglish(string baseName)
    {
        var root = FindRepositoryRoot();
        var english = LoadResx(Path.Combine(root, baseName + ".resx"));

        foreach (var language in AppLanguages.Supported.Where(l => l.Code != AppLanguages.DefaultCode))
        {
            var translated = LoadResx(Path.Combine(root, $"{baseName}.{language.Code}.resx"));
            Assert.Equal(english.Keys.Order(), translated.Keys.Order());
            foreach (var (key, text) in english)
            {
                Assert.False(string.IsNullOrWhiteSpace(text), key);
                Assert.True(Placeholders(text) == Placeholders(translated[key]), $"{language.Code} {key}: {text} / {translated[key]}");
            }
        }
    }

    private static Dictionary<string, string> LoadResx(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty);

    /// <summary>リポジトリのルート (テストの出力フォルダからさかのぼって、TetherKeeper.slnx のあるフォルダ)。</summary>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TetherKeeper.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("TetherKeeper.slnx が見つかりません。");
    }

    [Fact]
    public void DayNames_HaveSevenDaysInEachLanguage()
    {
        foreach (var culture in AppLanguages.ResourceCultures)
            Assert.Equal(7, CoreStrings.ResourceManager.GetString(nameof(CoreStrings.Weekly_DayNames), culture)!.Split(',').Length);
    }

    [Fact]
    public void AppLanguages_StartWithDefaultLanguage_AndResolveOnlySupportedCodes()
    {
        Assert.Equal(AppLanguages.DefaultCode, AppLanguages.Supported[0].Code);
        Assert.Equal("ja", AppLanguages.Resolve("ja")?.Name);
        Assert.Equal("ja", AppLanguages.Resolve("JA")?.Name);
        Assert.Null(AppLanguages.Resolve(""));
        Assert.Null(AppLanguages.Resolve("fr"));
        Assert.Null(AppLanguages.Resolve(null));
    }

    [Fact]
    public void Settings_Normalize_ClearsUnsupportedLanguage()
    {
        Assert.Equal(string.Empty, new AppSettings().Language);
        Assert.Equal("ja", new AppSettings { Language = " JA " }.Normalize().Language);
        Assert.Equal(string.Empty, new AppSettings { Language = "fr" }.Normalize().Language);
    }

    [Fact]
    public void ResultCsv_ReadsLinesWrittenInEitherLanguage()
    {
        var zone = TimeZoneInfo.Utc;

        var english = ResultCsv.Parse("2026/11/24 09:00:00,Succeeded,Ping,1.1.1.1,12,Fallback,Reply 12 ms", zone);
        var japanese = ResultCsv.Parse("2026/11/24 09:00:00,失敗,Ping,1.1.1.1,3000,,タイムアウト", zone);

        Assert.True(english?.Success);
        Assert.True(english?.UsedFallback);
        Assert.False(japanese?.Success);
    }

    [Fact]
    public void Validate_ReturnsJapaneseMessages_InTests()
    {
        // テストでは日本語の文言になる (TestCulture)
        Assert.Contains(new AppSettings { IntervalSeconds = 1 }.Validate(), e => e.Contains("実行間隔"));
    }
}
