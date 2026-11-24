using KeepAlive.Core.Services;

namespace KeepAlive.Tests.Services;

/// <summary>
/// 起動のいちばん初めに設定ファイルから表示言語を読む処理 (Windows 版と macOS 版で共通)。
/// 表示言語を変える <see cref="AppLanguageStartup.Apply"/> は、ほかのテストの文言に影響するため、ここでは確かめない。
/// </summary>
public sealed class AppLanguageStartupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KeepAliveTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "settings.json");

    public AppLanguageStartupTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void ReadSavedLanguage_ReturnsLanguage()
    {
        File.WriteAllText(FilePath, """{ "Language": "ja", "IntervalSeconds": 30 }""");

        Assert.Equal("ja", AppLanguageStartup.ReadSavedLanguage(FilePath));
    }

    [Fact]
    public void ReadSavedLanguage_ReturnsNull_WhenFileMissing()
    {
        Assert.Null(AppLanguageStartup.ReadSavedLanguage(FilePath));
    }

    [Theory]
    [InlineData("""{ "IntervalSeconds": 30 }""")]
    [InlineData("""{ "Language": 1 }""")]
    [InlineData("""[ "ja" ]""")]
    [InlineData("{ broken")]
    public void ReadSavedLanguage_ReturnsNull_WhenLanguageCannotBeRead(string json)
    {
        File.WriteAllText(FilePath, json);

        Assert.Null(AppLanguageStartup.ReadSavedLanguage(FilePath));
    }
}
