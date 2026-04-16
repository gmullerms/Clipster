using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IAiService _aiService;
    private readonly IAnimationService _animationService;
    private readonly IScreenCaptureService? _screenCaptureService;
    private readonly IOcrService? _ocrService;
    private readonly List<ChatMessage> _conversationHistory = new();

    [ObservableProperty]
    private string _userInput = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready to help!";

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public ChatViewModel(
        IAiService aiService,
        IAnimationService animationService,
        IScreenCaptureService? screenCaptureService = null,
        IOcrService? ocrService = null)
    {
        _aiService = aiService;
        _animationService = animationService;
        _screenCaptureService = screenCaptureService;
        _ocrService = ocrService;
    }

    public bool CanAnalyzeScreen => _screenCaptureService != null;

    public void PrePopulate(string message)
    {
        UserInput = message;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;

        var userMessage = new ChatMessage
        {
            Role = ChatRole.User,
            Content = UserInput.Trim()
        };

        Messages.Add(userMessage);
        _conversationHistory.Add(userMessage);
        UserInput = string.Empty;
        IsBusy = true;
        StatusText = "Thinking...";
        _animationService.TransitionTo(AnimationState.Thinking);

        try
        {
            var historyToSend = _conversationHistory.Count > 20
                ? _conversationHistory.GetRange(_conversationHistory.Count - 20, 20)
                : _conversationHistory;

            var response = await _aiService.ChatAsync(historyToSend);

            var assistantMessage = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = response
            };

            Messages.Add(assistantMessage);
            _conversationHistory.Add(assistantMessage);
            StatusText = "Ready to help!";
            _animationService.PlayOnce(AnimationState.Talking);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = "Oops! I need an API key to think. Please set it in Settings (right-click my tray icon)!"
            });
            StatusText = "API key needed";
            _animationService.TransitionTo(AnimationState.Confused);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = $"Sorry, my brain hiccuped! Error: {ex.Message}\nTry again in a moment?"
            });
            StatusText = "Error occurred";
            _animationService.TransitionTo(AnimationState.Confused);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeScreen()
    {
        if (_screenCaptureService == null) return;

        IsBusy = true;
        StatusText = "Looking at your screen...";
        _animationService.TransitionTo(AnimationState.Looking);

        Messages.Add(new ChatMessage
        {
            Role = ChatRole.User,
            Content = "[Analyzing screen...]"
        });

        try
        {
            // Capture screen
            var screenshot = await Task.Run(() => _screenCaptureService.CaptureFullScreen());

            // Run OCR if available
            var ocrText = string.Empty;
            if (_ocrService != null)
            {
                var ocrResult = await _ocrService.RecognizeAsync(screenshot);
                ocrText = ocrResult.Text;
            }

            StatusText = "Thinking about what I see...";
            _animationService.TransitionTo(AnimationState.Thinking);

            // Send to GPT-4o vision
            var response = await _aiService.AnalyzeScreenAsync(screenshot, ocrText);

            var assistantMessage = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = response
            };
            Messages.Add(assistantMessage);
            _conversationHistory.Add(assistantMessage);
            StatusText = "Ready to help!";
            _animationService.PlayOnce(AnimationState.Talking);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = "I need an API key to see! Please set it in Settings."
            });
            StatusText = "API key needed";
            _animationService.TransitionTo(AnimationState.Confused);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = $"Oops, I couldn't read the screen! Error: {ex.Message}"
            });
            StatusText = "Error occurred";
            _animationService.TransitionTo(AnimationState.Confused);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(UserInput);
    private bool CanAnalyze() => !IsBusy && _screenCaptureService != null;

    partial void OnUserInputChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        AnalyzeScreenCommand.NotifyCanExecuteChanged();
    }
}
