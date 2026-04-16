using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using Clipster.ViewModels;

namespace Clipster.App.Views;

public partial class ChatWindow : Window
{
    private readonly ChatViewModel _viewModel;

    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
        _viewModel.StreamingUpdate += OnStreamingUpdate;
        Loaded += (_, _) => InputBox.Focus();

        // Enable drag-drop
        AllowDrop = true;
        Drop += OnDrop;
        DragOver += OnDragOver;
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            MessageScroller.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnStreamingUpdate(object? sender, string text)
    {
        Dispatcher.InvokeAsync(() =>
        {
            MessageScroller.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            if (_viewModel.SendCommand.CanExecute(null))
            {
                _viewModel.SendCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            _viewModel.HandleFileDropCommand.Execute(files);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
        _viewModel.StreamingUpdate -= OnStreamingUpdate;
        base.OnClosed(e);
    }
}
