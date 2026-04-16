using Clipster.Core.Models;

namespace Clipster.Core.Events;

public class ClipboardChangedEventArgs : EventArgs
{
    public ClipboardContent Content { get; init; } = null!;
}
