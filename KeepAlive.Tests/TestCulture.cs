using System.Globalization;
using System.Runtime.CompilerServices;
using KeepAlive.Core.Resources;

namespace KeepAlive.Tests;

internal static class TestCulture
{
    /// <summary>
    /// テストは、文言を日本語で確かめる。PC や CI の表示言語に左右されないよう、テストの開始前に日本語に決める。
    /// (英語の文言は <see cref="Resources.LocalizationTests"/> で、日本語と同じ項目がそろっているかを確かめる)
    /// </summary>
    [ModuleInitializer]
    internal static void UseJapanese()
    {
        CoreStrings.Culture = CultureInfo.GetCultureInfo("ja-JP");
    }
}
