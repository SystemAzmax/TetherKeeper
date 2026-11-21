using System.Globalization;
using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Services;

/// <summary>
/// <see cref="AppDisplayName"/> のテスト。
/// 表示言語に合わせて、日本語名と英語名を切り替えることを検証します。
/// </summary>
public class AppDisplayNameTests
{
    /// <summary>
    /// 表示言語が日本語の場合は日本語名、それ以外の場合は英語名を返すことを検証します。
    /// </summary>
    /// <param name="cultureName">表示言語。</param>
    /// <param name="expected">期待するアプリ名。</param>
    [Theory]
    [InlineData("ja-JP", "テザリングキーパー")]
    [InlineData("ja", "テザリングキーパー")]
    [InlineData("en-US", "TetherKeeper")]
    [InlineData("en-GB", "TetherKeeper")]
    [InlineData("zh-CN", "TetherKeeper")]
    [InlineData("de-DE", "TetherKeeper")]
    [InlineData("", "TetherKeeper")]
    public void Get_表示言語_日本語なら日本語名でそれ以外は英語名を返す(
        string cultureName,
        string expected)
    {
        // Act
        var name = AppDisplayName.Get(new CultureInfo(cultureName));

        // Assert
        Assert.Equal(expected, name);
    }

    /// <summary>
    /// 現在の表示言語に合ったアプリ名を返すことを検証します。
    /// </summary>
    [Fact]
    public void GetCurrent_表示言語が日本語の場合_日本語名を返す()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");

        try
        {
            // Act
            var name = AppDisplayName.GetCurrent();

            // Assert
            Assert.Equal("テザリングキーパー", name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
