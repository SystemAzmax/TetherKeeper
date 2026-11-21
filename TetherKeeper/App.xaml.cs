using TetherKeeper.Services;

namespace TetherKeeper
{
    public partial class App : Application
    {
        private readonly AppShell _shell;

        public App(AppShell shell)
        {
            InitializeComponent();
            _shell = shell;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_shell)
            {
                // タイトルバーとタスクバーに出るアプリ名。表示言語に合わせて日本語名と英語名を切り替える
                Title = AppInfoProvider.GetProductName(),
                Width = 480,
                Height = 760,
                MinimumWidth = 380,
                MinimumHeight = 520,
            };
        }
    }
}
