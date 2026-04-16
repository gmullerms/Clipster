using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    private IHost? _host;
    private TaskbarIcon? _trayIcon;
    private ClipsterOverlayWindow? _overlayWindow;
    private GlobalHotkeyService? _hotkeyService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipster", "crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            System.IO.File.WriteAllText(logPath, $"{DateTime.Now}\n{args.Exception}\n");
            Console.Error.WriteLine(args.Exception);
            args.Handled = true;
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

            // Register global hotkey (Ctrl+Shift+Space)
            _hotkeyService = new GlobalHotkeyService();
            _hotkeyService.HotkeyPressed += OnQuickPromptHotkey;
            _hotkeyService.Register(_overlayWindow);

            // Setup system tray
            SetupTrayIcon();
        }
        catch (Exception ex)
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipster", "crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            System.IO.File.WriteAllText(logPath, $"{DateTime.Now}\n{ex}\n");
            MessageBox.Show($"Clipster failed to start:\n\n{ex.Message}", "Clipster Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IAiService, OpenAiService>();
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
        // Show the quick prompt dialog
        var promptWindow = new QuickPromptWindow();
        promptWindow.ShowDialog();

        if (!promptWindow.Submitted || string.IsNullOrWhiteSpace(promptWindow.PromptText))
            return;

        var aiService = _host!.Services.GetRequiredService<IAiService>();
        var animationService = _host.Services.GetRequiredService<IAnimationService>();
        var windowManager = _host.Services.GetRequiredService<IWindowManager>();

        // Show thinking state
        animationService.TransitionTo(AnimationState.Thinking);
        windowManager.ShowBubble("Let me think about that...", TimeSpan.FromSeconds(30));

        try
        {
            var result = await aiService.QuickPromptAsync(promptWindow.PromptText);

            // Copy the clean, paste-ready content to clipboard
            Clipboard.SetText(result.ClipboardContent);

            // Build the bubble message based on answer type
            var clipPreview = result.ClipboardContent.Length > 80
                ? result.ClipboardContent[..77] + "..."
                : result.ClipboardContent;

            string bubbleText;
            if (result.IsLongAnswer)
            {
                // Long answer: show note + hint to open chat
                bubbleText = $"Copied to clipboard!\n\n{result.Note}";
            }
            else
            {
                // Short answer: show what was copied + note
                bubbleText = $"Copied!\n\n> {clipPreview}";
                if (!string.IsNullOrWhiteSpace(result.Note) && result.Note != "Ready to paste!")
                    bubbleText += $"\n\n{result.Note}";
            }

            animationService.PlayOnce(AnimationState.Celebrating);

            var actions = new List<BubbleAction>
            {
                new() { Label = "OK", Callback = () => windowManager.HideBubble() },
            };

            // Add "See full answer" for long responses
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

        var quickItem = new System.Windows.Controls.MenuItem { Header = "Quick Prompt (Ctrl+Shift+Space)" };
        quickItem.Click += (_, _) => OnQuickPromptHotkey(null, EventArgs.Empty);
        menu.Items.Add(quickItem);

        var chatItem = new System.Windows.Controls.MenuItem { Header = "Open Chat" };
        chatItem.Click += (_, _) => _host?.Services.GetRequiredService<IWindowManager>().ShowChat();
        menu.Items.Add(chatItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => _host?.Services.GetRequiredService<IWindowManager>().ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var showItem = new System.Windows.Controls.MenuItem { Header = "Show Clipster" };
        showItem.Click += (_, _) => _overlayWindow?.Show();
        menu.Items.Add(showItem);

        var hideItem = new System.Windows.Controls.MenuItem { Header = "Hide Clipster" };
        hideItem.Click += (_, _) => _overlayWindow?.Hide();
        menu.Items.Add(hideItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) =>
        {
            _hotkeyService?.Dispose();
            _trayIcon?.Dispose();
            _overlayWindow?.Close();
            Shutdown();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
