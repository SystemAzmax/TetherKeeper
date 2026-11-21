namespace TetherKeeper
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // アプリ名は表示言語に合わせて日本語名と英語名を切り替えるため、XAML に書かずにここで設定する
            Title = Services.AppInfoProvider.GetProductName();
        }
    }
}
