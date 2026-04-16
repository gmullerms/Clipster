using Clipster.Core.Models;

namespace Clipster.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Settings { get; }
    event EventHandler? SettingsChanged;
    Task LoadAsync();
    Task SaveAsync();
}
