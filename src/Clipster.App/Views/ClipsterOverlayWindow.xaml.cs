using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Clipster.Core.Events;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;
using Clipster.Services.Animation;
using Clipster.Services.Clipboard;
using Clipster.ViewModels;

namespace Clipster.App.Views;

public partial class ClipsterOverlayWindow : Window
{
    private readonly ClipsterOverlayViewModel _viewModel;
    private readonly IWindowManager _windowManager;
    private readonly IAnimationService _animationService;
    private readonly ISettingsService _settingsService;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private readonly IClipboardService? _clipboardService;
    private readonly ITipService? _tipService;
    private DispatcherTimer? _topmostTimer;
    private DateTime _lastMouseDown;

    public ClipsterOverlayWindow(
        ClipsterOverlayViewModel viewModel,
        IWindowManager windowManager,
        IAnimationService animationService,
        ISettingsService settingsService,
        IClipboardService? clipboardService = null,
        ITipService? tipService = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowManager = windowManager;
        _animationService = animationService;
        _settingsService = settingsService;
        _clipboardService = clipboardService;
        _tipService = tipService;
        DataContext = viewModel;

        // Subscribe to animation state changes
        _animationService.StateChanged += OnAnimationStateChanged;

        // Subscribe to character completion events
        VectorClipster.AnimationCompleted += OnCharacterAnimationCompleted;
        ClassicClipster.AnimationCompleted += OnCharacterAnimationCompleted;

        // Subscribe to settings changes for character style
        _settingsService.SettingsChanged += OnSettingsChanged;

        // Subscribe to clipboard changes
        if (_clipboardService != null)
            _clipboardService.ClipboardChanged += OnClipboardChanged;

        // Subscribe to proactive tips
        if (_tipService != null)
            _tipService.TipReady += OnTipReady;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Apply character style
        ApplyCharacterStyle(_settingsService.Settings.CharacterStyle);

        // Position: use saved position if within screen bounds, else default to bottom-right
        var workArea = SystemParameters.WorkArea;
        var posX = _viewModel.PositionX;
        var posY = _viewModel.PositionY;
        var onScreen = !double.IsNaN(posX) && !double.IsNaN(posY)
                       && posX >= workArea.Left - 20 && posX <= workArea.Right
                       && posY >= workArea.Top - 20 && posY <= workArea.Bottom;

        if (onScreen)
        {
            Left = posX;
            Top = posY;
        }
        else
        {
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        }

        // Enforce topmost via Win32 and keep enforcing periodically
        EnforceTopmost();
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _topmostTimer.Tick += (_, _) => EnforceTopmost();
        _topmostTimer.Start();

        // Start clipboard monitoring
        if (_clipboardService != null && _settingsService.Settings.EnableClipboardMonitoring)
        {
            var helper = new WindowInteropHelper(this);
            _clipboardService.StartMonitoring(helper.Handle);
        }

        // Start proactive tips
        if (_tipService != null && _settingsService.Settings.EnableProactiveTips)
        {
            var s = _settingsService.Settings;
            _tipService.ConfigureInterval(
                TimeSpan.FromMinutes(s.TipIntervalMinMinutes),
                TimeSpan.FromMinutes(s.TipIntervalMaxMinutes));
            _tipService.Start();
        }

        // Play greeting animation then show bubble
        _animationService.PlayOnce(AnimationState.Greeting, () =>
        {
            Dispatcher.Invoke(() =>
            {
                _windowManager.ShowBubble(
                    "Hi! I'm Clipster! Click me to chat, or I'll pop up with tips!",
                    TimeSpan.FromSeconds(8));
            });
        });
    }

    private void EnforceTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _topmostTimer?.Stop();
        _tipService?.Stop();
        if (_tipService != null)
            _tipService.TipReady -= OnTipReady;
        _clipboardService?.StopMonitoring();
        if (_clipboardService != null)
            _clipboardService.ClipboardChanged -= OnClipboardChanged;
        _animationService.StateChanged -= OnAnimationStateChanged;
        VectorClipster.AnimationCompleted -= OnCharacterAnimationCompleted;
        ClassicClipster.AnimationCompleted -= OnCharacterAnimationCompleted;
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }

    private void OnTipReady(object? sender, TipReadyEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_settingsService.Settings.EnableProactiveTips)
                return;

            var actions = new List<BubbleAction>
            {
                new() { Label = "Tell me more", Callback = () => _windowManager.ShowChat(e.TipText + "\n\nTell me more about this!") },
                new() { Label = "Thanks!", Callback = () => _windowManager.HideBubble() },
                new() { Label = "Shh (1 hr)", Callback = () => { _tipService?.Snooze(TimeSpan.FromHours(1)); _windowManager.HideBubble(); } },
            };

            _animationService.PlayOnce(AnimationState.Greeting);
            _windowManager.ShowBubble(e.TipText, TimeSpan.FromSeconds(20), actions);
        });
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        if (!_settingsService.Settings.EnableClipboardMonitoring)
            return;

        Dispatcher.Invoke(() =>
        {
            var (message, actions) = ClipboardSuggestionProvider.GetSuggestions(
                e.Content,
                prompt => _windowManager.ShowChat(prompt));

            _animationService.PlayOnce(AnimationState.Pointing);
            _windowManager.ShowBubble(message, TimeSpan.FromSeconds(15), actions);
        });
    }

    private void OnAnimationStateChanged(object? sender, AnimationState state)
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsService.Settings.CharacterStyle == CharacterStyle.Modern)
                VectorClipster.PlayState(state);
            else
                ClassicClipster.PlayState(state);
        });
    }

    private void OnCharacterAnimationCompleted(object? sender, EventArgs e)
    {
        if (_animationService is AnimationService service)
        {
            service.OnAnimationCompleted();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => ApplyCharacterStyle(_settingsService.Settings.CharacterStyle));
    }

    private void ApplyCharacterStyle(CharacterStyle style)
    {
        if (style == CharacterStyle.Modern)
        {
            VectorClipster.Visibility = Visibility.Visible;
            ClassicClipster.Visibility = Visibility.Collapsed;
            VectorClipster.PlayState(_animationService.CurrentState);
        }
        else
        {
            VectorClipster.Visibility = Visibility.Collapsed;
            ClassicClipster.Visibility = Visibility.Visible;
            ClassicClipster.PlayState(_animationService.CurrentState);
        }
    }

    public void ShowSpeechBubble(string text, TimeSpan? autoHide = null, IReadOnlyList<BubbleAction>? actions = null)
    {
        SpeechBubble.Show(text, autoHide, actions);
    }

    public void HideSpeechBubble()
    {
        SpeechBubble.Hide();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastMouseDown = DateTime.Now;
        DragMove();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _viewModel.PositionX = Left;
        _viewModel.PositionY = Top;
        _viewModel.SavePositionCommand.Execute(null);
    }

    private void Clipster_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((DateTime.Now - _lastMouseDown).TotalMilliseconds < 200)
        {
            _animationService.PlayOnce(AnimationState.Greeting);
            _windowManager.ShowChat();
        }
    }
}
