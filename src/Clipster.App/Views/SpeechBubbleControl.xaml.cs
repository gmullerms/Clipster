using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clipster.Core.Interfaces;

namespace Clipster.App.Views;

public partial class SpeechBubbleControl : UserControl
{
    private DispatcherTimer? _autoHideTimer;

    public SpeechBubbleControl()
    {
        InitializeComponent();
    }

    public void Show(string text, TimeSpan? autoHide = null, IReadOnlyList<BubbleAction>? actions = null)
    {
        _autoHideTimer?.Stop();

        BubbleText.Text = text;
        ActionButtons.Items.Clear();

        if (actions != null)
        {
            foreach (var action in actions)
            {
                var btn = new Button
                {
                    Content = action.Label,
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(10, 4, 10, 4),
                    FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = System.Windows.Media.Brushes.White,
                    BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD4A017")),
                    BorderThickness = new Thickness(1)
                };
                var callback = action.Callback;
                btn.Click += (_, _) =>
                {
                    callback();
                    Hide();
                };
                ActionButtons.Items.Add(btn);
            }
        }

        Visibility = Visibility.Visible;

        if (autoHide.HasValue)
        {
            _autoHideTimer = new DispatcherTimer { Interval = autoHide.Value };
            _autoHideTimer.Tick += (_, _) =>
            {
                Hide();
                _autoHideTimer.Stop();
            };
            _autoHideTimer.Start();
        }
    }

    public void Hide()
    {
        _autoHideTimer?.Stop();
        Visibility = Visibility.Collapsed;
    }
}
