using KeepAlive.Core.Interfaces;
using TetherKeeper.Services;
using TetherKeeper.ViewModels;

namespace TetherKeeper.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly ILogService _log;

    public SettingsPage(SettingsViewModel viewModel, ILogService log)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _log = log;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.ReloadAsync();
    }

    /// <summary>
    /// 「このアプリについて」ボタンのクリックイベントを処理します。
    /// アプリ名・バージョン・著作権表記をダイアログで表示します。
    /// </summary>
    /// <param name="sender">イベント発生元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnAboutButtonClick(object? sender, EventArgs e)
    {
        await ShowMessageAsync(
            Text.Format(AppStrings.About_Title, AppInfoProvider.GetProductName()),
            AppInfoProvider.CreateAboutMessage());
    }

    /// <summary>
    /// 「ライセンス情報」ボタンのクリックイベントを処理します。
    /// 使用しているソフトウェアのライセンス表示のファイルを、既定のアプリで開きます。
    /// </summary>
    /// <param name="sender">イベント発生元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnThirdPartyNoticesButtonClick(object? sender, EventArgs e)
    {
        var path = AppInfoProvider.ThirdPartyNoticesPath;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error(Text.Format(AppStrings.Licenses_OpenFailedLog, ex.Message));
            await ShowMessageAsync(
                AppStrings.Settings_Licenses,
                Text.Format(AppStrings.Licenses_OpenFailed, path, ex.Message));
        }
    }

    /// <summary>
    /// 「使い方」ボタンのクリックイベントを処理します。
    /// 表示言語の使い方ガイドを、既定のブラウザーで開きます。
    /// </summary>
    /// <param name="sender">イベント発生元。</param>
    /// <param name="e">イベント引数。</param>
    private async void OnUserGuideButtonClick(object? sender, EventArgs e)
    {
        var url = AppInfoProvider.UserGuideUrl;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error(Text.Format(AppStrings.UserGuide_OpenFailedLog, ex.Message));
            await ShowMessageAsync(
                AppStrings.Settings_UserGuide,
                Text.Format(AppStrings.UserGuide_OpenFailed, url, ex.Message));
        }
    }

    /// <summary>
    /// タイトルと本文を設定したダイアログを、OK ボタン付きで表示します。
    /// </summary>
    /// <param name="title">ダイアログのタイトル。</param>
    /// <param name="message">ダイアログの本文。</param>
    /// <returns>ダイアログを閉じるまで待つタスク。</returns>
    private async Task ShowMessageAsync(string title, string message)
    {
        try
        {
            await DisplayAlertAsync(title, message, "OK");
        }
        catch (Exception)
        {
            // ほかのダイアログを表示中などで表示できないときは、何もしない
            // (async void から呼ばれるため、例外を外に出すとアプリが落ちる)
        }
    }
}
