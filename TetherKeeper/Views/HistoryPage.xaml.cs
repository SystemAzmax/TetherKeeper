using TetherKeeper.ViewModels;

namespace TetherKeeper.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // 開くたびに最新の記録で集計し直す
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.ReloadAsync();
    }
}
