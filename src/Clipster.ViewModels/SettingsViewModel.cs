using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelName = "gpt-4o";

    [ObservableProperty]
    private CharacterStyle _characterStyle;

    [ObservableProperty]
    private bool _enableClipboardMonitoring;

    [ObservableProperty]
    private bool _enableProactiveTips;

    [ObservableProperty]
    private bool _enableOcr;

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
        ApiKey = s.ApiKey;
        ModelName = s.ModelName;
        CharacterStyle = s.CharacterStyle;
        EnableClipboardMonitoring = s.EnableClipboardMonitoring;
        EnableProactiveTips = s.EnableProactiveTips;
        EnableOcr = s.EnableOcr;
    }

    [RelayCommand]
    private async Task Save()
    {
        var s = _settings.Settings;
        s.ApiKey = ApiKey;
        s.ModelName = ModelName;
        s.CharacterStyle = CharacterStyle;
        s.EnableClipboardMonitoring = EnableClipboardMonitoring;
        s.EnableProactiveTips = EnableProactiveTips;
        s.EnableOcr = EnableOcr;

        await _settings.SaveAsync();
        StatusMessage = "Settings saved!";
    }
}
