using System.Windows;
using System.Windows.Input;

namespace Clipster.App.Views;

public partial class QuickPromptWindow : Window
{
    public string? PromptText { get; private set; }
    public bool Submitted { get; private set; }
    private bool _closing;

    public QuickPromptWindow()
    {
        InitializeComponent();
        ContentRendered += (_, _) =>
        {
            Activate();
            PromptInput.Focus();
            Keyboard.Focus(PromptInput);
        };
    }

    private void PromptInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            PromptText = PromptInput.Text.Trim();
            Submitted = true;
            SafeClose();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SafeClose();
            e.Handled = true;
        }
    }

    private void PromptInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(PromptInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!Submitted)
            SafeClose();
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }
}
