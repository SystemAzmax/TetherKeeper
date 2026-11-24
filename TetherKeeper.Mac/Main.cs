using AppKit;

namespace TetherKeeper.Mac;

public static class Program
{
    // NSApplication の Delegate は弱い参照のため、ガベージ コレクションで消えないようここで持つ
    private static AppDelegate? _delegate;

    public static void Main(string[] args)
    {
        NSApplication.Init();
        _delegate = new AppDelegate();
        NSApplication.SharedApplication.Delegate = _delegate;
        NSApplication.Main(args);
    }
}
