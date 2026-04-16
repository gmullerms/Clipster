using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    // Provider
    [ObservableProperty]
    private LlmProvider _provider;

    // OpenAI
    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelName = "gpt-4o";

    // Claude
    [ObservableProperty]
    private string _claudeApiKey = string.Empty;

    [ObservableProperty]
    private string _claudeModelName = "claude-sonnet-4-20250514";

    // Ollama
    [ObservableProperty]
    private string _ollamaEndpoint = "http://localhost:11434";

    [ObservableProperty]
    private string _ollamaModelName = "llama3";

    // Character
    [ObservableProperty]
    private CharacterStyle _characterStyle;

    // Features
    [ObservableProperty]
    private bool _enableClipboardMonitoring;

    [ObservableProperty]
    private bool _enableProactiveTips;

    [ObservableProperty]
    private bool _enableOcr;

    [ObservableProperty]
    private bool _enableScreenWatcher;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settings.Settings;
        Provider = s.Provider;
        ApiKey = s.ApiKey;
        ModelName = s.ModelName;
        ClaudeApiKey = s.ClaudeApiKey;
        ClaudeModelName = s.ClaudeModelName;
        OllamaEndpoint = s.OllamaEndpoint;
        OllamaModelName = s.OllamaModelName;
        CharacterStyle = s.CharacterStyle;
        EnableClipboardMonitoring = s.EnableClipboardMonitoring;
        EnableProactiveTips = s.EnableProactiveTips;
        EnableOcr = s.EnableOcr;
        EnableScreenWatcher = s.EnableScreenWatcher;
    }

    [RelayCommand]
    private async Task Save()
    {
        var s = _settings.Settings;
        s.Provider = Provider;
        s.ApiKey = ApiKey;
        s.ModelName = ModelName;
        s.ClaudeApiKey = ClaudeApiKey;
        s.ClaudeModelName = ClaudeModelName;
        s.OllamaEndpoint = OllamaEndpoint;
        s.OllamaModelName = OllamaModelName;
        s.CharacterStyle = CharacterStyle;
        s.EnableClipboardMonitoring = EnableClipboardMonitoring;
        s.EnableProactiveTips = EnableProactiveTips;
        s.EnableOcr = EnableOcr;
        s.EnableScreenWatcher = EnableScreenWatcher;

        await _settings.SaveAsync();
        StatusMessage = "Settings saved!";
    }
}
