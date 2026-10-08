namespace TetherKeeper.Services;

/// <summary>
/// アプリが使うファイル・フォルダの場所。
/// <list type="bullet">
/// <item>Windows 非パッケージ版 (開発時): %LOCALAPPDATA%\TetherKeeper
/// (MAUI 標準の FileSystem.AppDataDirectory は %LOCALAPPDATA%\発行者名\アプリID\Data という分かりにくい場所になるため使わない)</item>
/// <item>MSIX パッケージ版: パッケージ専用の LocalState フォルダ (アンインストール時に自動で削除される)</item>
/// </list>
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "TetherKeeper";

    public static string DataDirectory => OperatingSystem.IsWindows() && !AppPackage.IsPackaged
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName)
        : FileSystem.AppDataDirectory;

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
}
