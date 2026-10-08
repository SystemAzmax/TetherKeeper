using System.Runtime.InteropServices;

namespace TetherKeeper.Services;

/// <summary>
/// MSIX パッケージとしてインストールされた状態で実行されているかを判定する。
/// 開発時 (Visual Studio からのデバッグ実行や exe の直接実行) は非パッケージ、
/// MSIX からインストールした場合はパッケージとして動作し、自動起動や保存先の仕組みが変わる。
/// </summary>
public static class AppPackage
{
    private const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        // パッケージ外で実行していると APPMODEL_ERROR_NO_PACKAGE が返る
        // (パッケージ内ならバッファ不足エラーになるが、ここでは名前自体は不要)
        var length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage;
    }

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);
}
