using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TetherKeeper.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // 既に起動中 (トレイ常駐中を含む) なら、そちらのウィンドウを表示させて自分は終了する
            if (!SingleInstance.TryAcquire())
            {
                SingleInstance.SignalExistingInstance();
                Environment.Exit(0);
            }

            this.InitializeComponent();
        }

        /// <summary>タスクトレイ制御。GC に回収されないよう保持する。</summary>
        internal static TrayController? Tray { get; set; }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
