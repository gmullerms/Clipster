using System.IO;
using System.Windows;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;
using Clipster.Services.AI;
using Clipster.Services.Animation;
using Clipster.Services.Clipboard;
using Clipster.Services.Ocr;
using Clipster.Services.Settings;
using Clipster.Services.Tips;
using Clipster.Services.Window;
using Clipster.ViewModels;
using Clipster.App.Views;
using Clipster.App.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace Clipster.App;

public partial class App : Application
{
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Clipster";

    private IHost? _host;
    private TaskbarIcon? _trayIcon;
    private ClipsterOverlayWindow? _overlayWindow;
    private GlobalHotkeyService? _hotkeyService;
    private ScreenWatcherService? _screenWatcher;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipster");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "crash.log");

        // Log every exit to understand why the app closes
        Exit += (_, exitArgs) =>
        {
            File.AppendAllText(logPath, $"\n[{DateTime.Now}] APP EXIT code={exitArgs.ApplicationExitCode}\n{Environment.StackTrace}\n");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            File.AppendAllText(logPath, $"\n[{DateTime.Now}] UI THREAD:\n{args.Exception}\n");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            File.AppendAllText(logPath, $"\n[{DateTime.Now}] BACKGROUND THREAD:\n{args.ExceptionObject}\n");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            File.AppendAllText(logPath, $"\n[{DateTime.Now}] UNOBSERVED TASK:\n{args.Exception}\n");
            args.SetObserved();
        };

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            // Load settings first
            var settings = _host.Services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();

            // Create and show the overlay window
            _overlayWindow = _host.Services.GetRequiredService<ClipsterOverlayWindow>();
            _overlayWindow.Show();

            // Register global hotkeys
            _hotkeyService = new GlobalHotkeyService();
            _hotkeyService.QuickPromptPressed += OnQuickPromptHotkey;
            _hotkeyService.ScreenScanPressed += OnScreenScanHotkey;
            _hotkeyService.Register(_overlayWindow);

            // Start screen watcher (periodic background analysis)
            _screenWatcher = new ScreenWatcherService(
                _host.Services.GetRequiredService<IScreenCaptureService>(),
                _host.Services.GetRequiredService<IAiService>());
            _screenWatcher.InsightReady += OnScreenInsightReady;
            if (!string.IsNullOrWhiteSpace(settings.Settings.ApiKey))
                _screenWatcher.Start(TimeSpan.FromMinutes(5));

            // Setup system tray
            SetupTrayIcon();

            // First launch: prompt for API key if not set
            if (string.IsNullOrWhiteSpace(settings.Settings.ApiKey))
            {
                await System.Threading.Tasks.Task.Delay(1500); // Let greeting animation finish
                var windowManager = _host.Services.GetRequiredService<IWindowManager>();
                windowManager.ShowBubble(
                    "Welcome! I need an OpenAI API key to get started. Click below to set it up!",
                    null,
                    new List<BubbleAction>
                    {
                        new() { Label = "Open Settings", Callback = () => windowManager.ShowSettings() },
                    });
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"\n[{DateTime.Now}] STARTUP:\n{ex}\n");
            MessageBox.Show($"Clipster failed to start:\n\n{ex.Message}", "Clipster Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddHttpClient();
        services.AddSingleton<IAiService>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            var httpFactory = sp.GetService<IHttpClientFactory>();
            return AiServiceFactory.Create(settings.Settings.Provider, settings, httpFactory);
        });
        services.AddSingleton<IAnimationService, AnimationService>();
        services.AddSingleton<IClipboardService, ClipboardMonitorService>();
        services.AddSingleton<ITipService, ProactiveTipService>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IOcrService, SimpleOcrService>();

        // Window manager with factory delegates
        services.AddSingleton<IWindowManager>(sp =>
        {
            return new WindowManagerService(
                chatWindowFactory: () =>
                {
                    var vm = sp.GetRequiredService<ChatViewModel>();
                    return new ChatWindow(vm);
                },
                settingsWindowFactory: () =>
                {
                    var vm = sp.GetRequiredService<SettingsViewModel>();
                    return new SettingsWindow(vm);
                },
                overlayWindowAccessor: () => _overlayWindow!
            );
        });

        // ViewModels
        services.AddTransient(sp => new ChatViewModel(
            sp.GetRequiredService<IAiService>(),
            sp.GetRequiredService<IAnimationService>(),
            sp.GetRequiredService<IScreenCaptureService>(),
            sp.GetRequiredService<IOcrService>()));
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<ClipsterOverlayViewModel>();

        // Views
        services.AddSingleton(sp =>
        {
            var vm = sp.GetRequiredService<ClipsterOverlayViewModel>();
            var wm = sp.GetRequiredService<IWindowManager>();
            var anim = sp.GetRequiredService<IAnimationService>();
            var settings = sp.GetRequiredService<ISettingsService>();
            var clipboard = sp.GetRequiredService<IClipboardService>();
            var tips = sp.GetRequiredService<ITipService>();
            return new ClipsterOverlayWindow(vm, wm, anim, settings, clipboard, tips);
        });
    }

    private async void OnQuickPromptHotkey(object? sender, EventArgs e)
    {
        var promptWindow = new QuickPromptWindow();
        promptWindow.ShowDialog();

        if (!promptWindow.Submitted || string.IsNullOrWhiteSpace(promptWindow.PromptText))
            return;

        var aiService = _host!.Services.GetRequiredService<IAiService>();
        var animationService = _host.Services.GetRequiredService<IAnimationService>();
        var windowManager = _host.Services.GetRequiredService<IWindowManager>();

        animationService.TransitionTo(AnimationState.Thinking);
        windowManager.ShowBubble("Let me think about that...", TimeSpan.FromSeconds(30));

        try
        {
            var result = await aiService.QuickPromptAsync(promptWindow.PromptText);

            Clipboard.SetText(result.ClipboardContent);

            var clipPreview = result.ClipboardContent.Length > 80
                ? result.ClipboardContent[..77] + "..."
                : result.ClipboardContent;

            string bubbleText;
            if (result.IsLongAnswer)
            {
                bubbleText = $"Copied to clipboard!\n\n{result.Note}";
            }
            else
            {
                bubbleText = $"Copied!\n\n> {clipPreview}";
                if (!string.IsNullOrWhiteSpace(result.Note) && result.Note != "Ready to paste!")
                    bubbleText += $"\n\n{result.Note}";
            }

            animationService.PlayOnce(AnimationState.Celebrating);

            var actions = new List<BubbleAction>
            {
                new() { Label = "OK", Callback = () => windowManager.HideBubble() },
            };

            if (result.IsLongAnswer)
            {
                actions.Insert(0, new BubbleAction
                {
                    Label = "See full answer",
                    Callback = () => windowManager.ShowChat(promptWindow.PromptText)
                });
            }

            windowManager.ShowBubble(bubbleText, TimeSpan.FromSeconds(12), actions);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            animationService.TransitionTo(AnimationState.Confused);
            windowManager.ShowBubble(
                "I need an API key first! Open Settings to set it up.",
                TimeSpan.FromSeconds(8),
                new List<BubbleAction>
                {
                    new() { Label = "Settings", Callback = () => windowManager.ShowSettings() },
                });
        }
        catch (Exception ex)
        {
            animationService.TransitionTo(AnimationState.Confused);
            windowManager.ShowBubble(
                $"Oops! Something went wrong: {ex.Message}",
                TimeSpan.FromSeconds(8));
        }
    }

    private async void OnScreenScanHotkey(object? sender, EventArgs e)
    {
        if (_screenWatcher == null) return;

        var animationService = _host!.Services.GetRequiredService<IAnimationService>();
        var windowManager = _host.Services.GetRequiredService<IWindowManager>();

        animationService.TransitionTo(AnimationState.Looking);
        windowManager.ShowBubble("Let me take a look at your screen...", TimeSpan.FromSeconds(20));

        try
        {
            var insight = await _screenWatcher.AnalyzeNowAsync();

            if (insight.ShouldNotify)
            {
                ShowInsightBubble(insight, animationService, windowManager);
            }
            else
            {
                animationService.PlayOnce(AnimationState.Talking);
                windowManager.ShowBubble(
                    "Everything looks good! I don't see any issues on your screen.",
                    TimeSpan.FromSeconds(6));
            }
        }
        catch (Exception ex)
        {
            animationService.TransitionTo(AnimationState.Confused);
            windowManager.ShowBubble($"Couldn't analyze the screen: {ex.Message}", TimeSpan.FromSeconds(8));
        }
    }

    private void OnScreenInsightReady(object? sender, ScreenInsight insight)
    {
        Dispatcher.Invoke(() =>
        {
            var animationService = _host!.Services.GetRequiredService<IAnimationService>();
            var windowManager = _host.Services.GetRequiredService<IWindowManager>();
            ShowInsightBubble(insight, animationService, windowManager);
        });
    }

    private void ShowInsightBubble(ScreenInsight insight, IAnimationService animationService, IWindowManager windowManager)
    {
        // Pick animation based on insight type
        var anim = insight.Type switch
        {
            InsightType.Error => AnimationState.Pointing,
            InsightType.Warning => AnimationState.Pointing,
            InsightType.Suggestion => AnimationState.Greeting,
            InsightType.Question => AnimationState.Greeting,
            _ => AnimationState.Talking
        };
        animationService.PlayOnce(anim);

        var actions = new List<BubbleAction>
        {
            new() { Label = "Help me!", Callback = () => windowManager.ShowChat(
                $"I noticed this on my screen: {insight.Summary}\n\n{insight.Detail}\n\nPlease help me with this.") },
            new() { Label = "Got it", Callback = () => windowManager.HideBubble() },
            new() { Label = "Shh (30m)", Callback = () => { _screenWatcher?.Snooze(TimeSpan.FromMinutes(30)); windowManager.HideBubble(); } },
        };

        var bubbleText = insight.Summary;
        if (!string.IsNullOrWhiteSpace(insight.Detail))
            bubbleText += $"\n\n{insight.Detail}";

        windowManager.ShowBubble(bubbleText, TimeSpan.FromSeconds(20), actions);
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Clipster - AI Assistant (Ctrl+Shift+Space)",
            ContextMenu = CreateTrayMenu()
        };
    }

    private System.Windows.Controls.ContextMenu CreateTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var quickItem = new System.Windows.Controls.MenuItem { Header = "Quick Prompt  (Ctrl+Shift+Space)" };
        quickItem.Click += (_, _) => OnQuickPromptHotkey(null, EventArgs.Empty);
        menu.Items.Add(quickItem);

        var scanItem = new System.Windows.Controls.MenuItem { Header = "Scan Screen  (Ctrl+Shift+S)" };
        scanItem.Click += (_, _) => OnScreenScanHotkey(null, EventArgs.Empty);
        menu.Items.Add(scanItem);

        var chatItem = new System.Windows.Controls.MenuItem { Header = "Open Chat" };
        chatItem.Click += (_, _) => _host?.Services.GetRequiredService<IWindowManager>().ShowChat();
        menu.Items.Add(chatItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // Animation demo submenu
        var animMenu = new System.Windows.Controls.MenuItem { Header = "Animations" };
        foreach (var state in Enum.GetValues<AnimationState>())
        {
            var s = state;
            var item = new System.Windows.Controls.MenuItem { Header = s.ToString() };
            item.Click += (_, _) =>
            {
                var anim = _host?.Services.GetRequiredService<IAnimationService>();
                if (s == AnimationState.Idle || s == AnimationState.Thinking || s == AnimationState.Looking)
                    anim?.TransitionTo(s);
                else
                    anim?.PlayOnce(s);
            };
            animMenu.Items.Add(item);
        }
        menu.Items.Add(animMenu);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var showItem = new System.Windows.Controls.MenuItem { Header = "Show Clipster" };
        showItem.Click += (_, _) => _overlayWindow?.Show();
        menu.Items.Add(showItem);

        var hideItem = new System.Windows.Controls.MenuItem { Header = "Hide Clipster" };
        hideItem.Click += (_, _) => _overlayWindow?.Hide();
        menu.Items.Add(hideItem);

        // Startup on boot toggle
        var startupItem = new System.Windows.Controls.MenuItem { Header = "Start with Windows" };
        startupItem.IsCheckable = true;
        startupItem.IsChecked = IsStartupEnabled();
        startupItem.Click += (_, _) =>
        {
            if (startupItem.IsChecked)
                EnableStartup();
            else
                DisableStartup();
        };
        menu.Items.Add(startupItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => _host?.Services.GetRequiredService<IWindowManager>().ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) =>
        {
            _screenWatcher?.Dispose();
            _hotkeyService?.Dispose();
            _trayIcon?.Dispose();
            _overlayWindow?.Close();
            Shutdown();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    private static void EnableStartup()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null) return;
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    private static void DisableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        key?.DeleteValue(AppName, false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _screenWatcher?.Dispose();
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
