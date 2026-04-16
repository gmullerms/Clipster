using Clipster.Core.Interfaces;

namespace Clipster.Services.Window;

public class WindowManagerService : IWindowManager
{
    private readonly Func<object> _chatWindowFactory;
    private readonly Func<object> _settingsWindowFactory;
    private readonly Func<object> _overlayWindowAccessor;

    private object? _chatWindow;
    private object? _settingsWindow;

    // We use Func<object> + dynamic dispatch to avoid referencing WPF types from the Services project.
    // The actual window types are registered in the App project's DI configuration.
    public WindowManagerService(
        Func<object> chatWindowFactory,
        Func<object> settingsWindowFactory,
        Func<object> overlayWindowAccessor)
    {
        _chatWindowFactory = chatWindowFactory;
        _settingsWindowFactory = settingsWindowFactory;
        _overlayWindowAccessor = overlayWindowAccessor;
    }

    public void ShowChat(string? prePopulatedMessage = null)
    {
        dynamic? window = _chatWindow;
        if (window == null || !IsWindowOpen(window))
        {
            window = _chatWindowFactory();
            _chatWindow = window;

            if (!string.IsNullOrWhiteSpace(prePopulatedMessage))
            {
                ((dynamic)window.DataContext).PrePopulate(prePopulatedMessage);
            }

            window.Show();
        }
        else
        {
            window!.Activate();
            if (!string.IsNullOrWhiteSpace(prePopulatedMessage))
            {
                ((dynamic)window.DataContext).PrePopulate(prePopulatedMessage);
            }
        }
    }

    public void HideChat()
    {
        dynamic? window = _chatWindow;
        if (window != null && IsWindowOpen(window))
        {
            window!.Close();
        }
        _chatWindow = null;
    }

    public void ShowSettings()
    {
        dynamic? window = _settingsWindow;
        if (window == null || !IsWindowOpen(window))
        {
            window = _settingsWindowFactory();
            _settingsWindow = window;
            window.Show();
        }
        else
        {
            window!.Activate();
        }
    }

    public void ShowBubble(string text, TimeSpan? autoHide = null, IReadOnlyList<BubbleAction>? actions = null)
    {
        dynamic overlay = _overlayWindowAccessor();
        overlay.ShowSpeechBubble(text, autoHide, actions);
    }

    public void HideBubble()
    {
        dynamic overlay = _overlayWindowAccessor();
        overlay.HideSpeechBubble();
    }

    private static bool IsWindowOpen(dynamic window)
    {
        try
        {
            return window.IsLoaded;
        }
        catch
        {
            return false;
        }
    }
}
