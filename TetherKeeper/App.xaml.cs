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
                Title = "TetherKeeper",
                Width = 480,
                Height = 760,
                MinimumWidth = 380,
                MinimumHeight = 520,
            };
        }
    }
}
