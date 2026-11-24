using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using KeepAlive.Core.Models;
using KeepAlive.Core.Resources;
using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Resources;

public partial class LocalizationTests
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja");

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
    public void CoreStrings_EnglishAndJapanese_HaveSameKeysAndPlaceholders()
    {
        var english = Load(CoreStrings.ResourceManager, CultureInfo.InvariantCulture);
        var japanese = Load(CoreStrings.ResourceManager, Japanese);

        Assert.Equal(english.Keys.Order(), japanese.Keys.Order());
        foreach (var (key, text) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(text), key);
            // 埋め込む値の番号がずれていると、言語によって表示が崩れたり例外になったりする
            Assert.True(Placeholders(text) == Placeholders(japanese[key]), $"{key}: {text} / {japanese[key]}");
        }
    }

    [Fact]
    public void DayNames_HaveSevenDaysInEachLanguage()
    {
        foreach (var culture in new[] { CultureInfo.InvariantCulture, Japanese })
            Assert.Equal(7, CoreStrings.ResourceManager.GetString(nameof(CoreStrings.Weekly_DayNames), culture)!.Split(',').Length);
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
