namespace Clipster.Core.Events;

public class TipReadyEventArgs : EventArgs
{
    public string TipText { get; init; } = string.Empty;
}
