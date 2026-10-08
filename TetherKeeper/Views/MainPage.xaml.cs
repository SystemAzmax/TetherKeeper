using TetherKeeper.ViewModels;

namespace TetherKeeper.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly IDispatcherTimer _countdownTimer;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _countdownTimer = Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += (_, _) => _viewModel.UpdateCountdown();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _countdownTimer.Start();
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        _countdownTimer.Stop();
        base.OnDisappearing();
    }
}
