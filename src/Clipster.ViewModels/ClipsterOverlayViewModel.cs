using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clipster.Core.Interfaces;

namespace Clipster.ViewModels;

public partial class ClipsterOverlayViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private double _positionX;

    [ObservableProperty]
    private double _positionY;

    public ClipsterOverlayViewModel(ISettingsService settings)
    {
        _settings = settings;
        _positionX = settings.Settings.ClipsterPositionX;
        _positionY = settings.Settings.ClipsterPositionY;
    }

    [RelayCommand]
    private async Task SavePosition()
    {
        _settings.Settings.ClipsterPositionX = PositionX;
        _settings.Settings.ClipsterPositionY = PositionY;
        await _settings.SaveAsync();
    }
}
