using System.Collections.ObjectModel;
using System.Text;
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

    [ObservableProperty]
    private string _tokenInfo = string.Empty;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    /// <summary>
    /// Fired during streaming with partial content updates for the last assistant message.
    /// The string is the full accumulated text so far.
    /// </summary>
    public event EventHandler<string>? StreamingUpdate;

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

            // Add a placeholder message for streaming
            var assistantMessage = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = ""
            };
            Messages.Add(assistantMessage);

            var sb = new StringBuilder();
            var firstChunk = true;

            await foreach (var chunk in _aiService.ChatStreamAsync(historyToSend))
            {
                if (firstChunk)
                {
                    _animationService.PlayOnce(AnimationState.Talking);
                    StatusText = "Responding...";
                    firstChunk = false;
                }

                sb.Append(chunk);
                var fullText = sb.ToString();

                // Update the message in-place
                Messages[^1] = new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = fullText,
                    Timestamp = assistantMessage.Timestamp
                };

                StreamingUpdate?.Invoke(this, fullText);
            }

            var finalContent = sb.ToString();
            _conversationHistory.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = finalContent
            });

            UpdateTokenInfo();
            StatusText = "Ready to help!";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            // Remove placeholder if it was added
            if (Messages.Count > 0 && Messages[^1].Role == ChatRole.Assistant && string.IsNullOrEmpty(Messages[^1].Content))
                Messages.RemoveAt(Messages.Count - 1);

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
            if (Messages.Count > 0 && Messages[^1].Role == ChatRole.Assistant && string.IsNullOrEmpty(Messages[^1].Content))
                Messages.RemoveAt(Messages.Count - 1);

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

        Messages.Add(new ChatMessage { Role = ChatRole.User, Content = "[Analyzing screen...]" });

        try
        {
            var screenshot = await Task.Run(() => _screenCaptureService.CaptureFullScreen());

            var ocrText = string.Empty;
            if (_ocrService != null)
            {
                var ocrResult = await _ocrService.RecognizeAsync(screenshot);
                ocrText = ocrResult.Text;
            }

            StatusText = "Thinking about what I see...";
            _animationService.TransitionTo(AnimationState.Thinking);

            var response = await _aiService.AnalyzeScreenAsync(screenshot, ocrText);

            Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Content = response });
            _conversationHistory.Add(new ChatMessage { Role = ChatRole.Assistant, Content = response });
            UpdateTokenInfo();
            StatusText = "Ready to help!";
            _animationService.PlayOnce(AnimationState.Talking);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Content = "I need an API key to see! Please set it in Settings." });
            StatusText = "API key needed";
            _animationService.TransitionTo(AnimationState.Confused);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Content = $"Oops! Error: {ex.Message}" });
            StatusText = "Error occurred";
            _animationService.TransitionTo(AnimationState.Confused);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void HandleFileDrop(string[] filePaths)
    {
        if (filePaths.Length == 0) return;

        var fileList = string.Join(", ", filePaths.Select(System.IO.Path.GetFileName));
        var ext = System.IO.Path.GetExtension(filePaths[0]).ToLowerInvariant();

        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp")
        {
            UserInput = $"[Dropped image: {fileList}]\nPlease analyze this image.";
        }
        else
        {
            // Read text files and include content
            try
            {
                var content = System.IO.File.ReadAllText(filePaths[0]);
                var preview = content.Length > 2000 ? content[..2000] + "\n... (truncated)" : content;
                UserInput = $"Here's the contents of {fileList}:\n\n```\n{preview}\n```\n\nPlease analyze this.";
            }
            catch
            {
                UserInput = $"I dropped a file: {fileList}. Please help me with it.";
            }
        }
    }

    private void UpdateTokenInfo()
    {
        var usage = _aiService.LastUsage;
        if (usage != null)
        {
            TokenInfo = $"{usage.TotalTokens} tokens (~${usage.EstimatedCost:F4})";
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
