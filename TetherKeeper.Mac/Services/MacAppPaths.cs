using Foundation;

namespace TetherKeeper.Mac.Services;

/// <summary>
/// アプリが使うファイル・フォルダの場所。
/// Application Support の下の TetherKeeper フォルダに、Windows 版と同じ構成で置く。
/// Mac App Store 版はサンドボックスで動くため、実際の場所は
/// ~/Library/Containers/&lt;バンドル ID&gt;/Data/Library/Application Support/TetherKeeper になる
/// (アプリを削除してもコンテナは残る。消すときは Finder でコンテナのフォルダを削除する)。
/// </summary>
public static class MacAppPaths
{
    private const string AppFolderName = "TetherKeeper";

    public static string DataDirectory { get; } = GetDataDirectory();

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string DataUsageFile => Path.Combine(DataDirectory, "data-usage.json");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>通信の結果の履歴 (日ごとの CSV) を置くフォルダ。</summary>
    public static string HistoryDirectory => Path.Combine(DataDirectory, "history");

    private static string GetDataDirectory()
    {
        var urls = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
        var baseDirectory = urls.Length > 0 && urls[0].Path is { Length: > 0 } path
            ? path
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDirectory, AppFolderName);
    }
}
