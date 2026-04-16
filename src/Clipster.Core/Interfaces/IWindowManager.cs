namespace Clipster.Core.Interfaces;

public interface IWindowManager
{
    void ShowChat(string? prePopulatedMessage = null);
    void HideChat();
    void ShowSettings();
    void ShowBubble(string text, TimeSpan? autoHide = null, IReadOnlyList<BubbleAction>? actions = null);
    void HideBubble();
}

public class BubbleAction
{
    public string Label { get; init; } = string.Empty;
    public Action Callback { get; init; } = () => { };
}
