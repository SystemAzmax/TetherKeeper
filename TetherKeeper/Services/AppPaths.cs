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

    public static string DataUsageFile => Path.Combine(DataDirectory, "data-usage.json");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>通信の結果の履歴 (日ごとの CSV) を置くフォルダ。</summary>
    public static string HistoryDirectory => Path.Combine(DataDirectory, "history");

    /// <summary>
    /// 履歴を書き出す先 (ダウンロード フォルダー)。
    /// MSIX 版の保存先 (LocalState) は奥深く分かりにくいため、利用者が見つけやすい場所に書き出す。
    /// </summary>
    public static string ExportDirectory
    {
        get
        {
#if WINDOWS
            // ダウンロード フォルダーは利用者が場所を変えられるため、決め打ちせずに Windows に問い合わせる
            if (SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out var pathPointer) == 0)
            {
                try
                {
                    if (System.Runtime.InteropServices.Marshal.PtrToStringUni(pathPointer) is { Length: > 0 } path)
                        return path;
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pathPointer);
                }
            }
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

#if WINDOWS
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid folderId,
        uint flags,
        IntPtr token,
        out IntPtr path);
#endif
}
