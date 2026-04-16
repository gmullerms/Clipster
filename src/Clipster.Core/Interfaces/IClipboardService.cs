using Clipster.Core.Events;

namespace Clipster.Core.Interfaces;

public interface IClipboardService
{
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    void StartMonitoring(IntPtr windowHandle);
    void StopMonitoring();
}
