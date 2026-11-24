using AppKit;

namespace TetherKeeper.Mac;

internal static class AppActivation
{
    /// <summary>
    /// アプリを前面に出す。メニューバー常駐のアプリ (LSUIElement) は、開いたウィンドウが他のアプリの後ろに隠れるため、
    /// ウィンドウを出す前に呼ぶ。
    /// </summary>
    public static void BringToFront()
    {
        // macOS 14 で非推奨になったが、13 でも動くこちらを使う (14 以降でも動く)
#pragma warning disable CA1422, CS0618
        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
#pragma warning restore CA1422, CS0618
    }
}
