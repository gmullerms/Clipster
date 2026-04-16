using System.Windows;
using System.Windows.Input;
using Clipster.ViewModels;

namespace Clipster.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) =>
        {
            ApiKeyBox.Password = _viewModel.ApiKey;
            ClaudeKeyBox.Password = _viewModel.ClaudeApiKey;
        };
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ApiKey = ApiKeyBox.Password;
    }

    private void ClaudeKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ClaudeApiKey = ClaudeKeyBox.Password;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
