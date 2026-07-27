using System.Windows;
using FModel.Localization;
using FModel.ViewModels;

namespace FModel.Views;

public partial class About
{
    private readonly AboutViewModel _viewModel;

    public About()
    {
        DataContext = _viewModel = new AboutViewModel();
        InitializeComponent();
        LocalizationManager.Apply(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.Initialize();
        LocalizationManager.Apply(this);
    }
}
