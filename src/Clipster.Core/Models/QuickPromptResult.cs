namespace Clipster.Core.Models;

public class QuickPromptResult
{
    public string ClipboardContent { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public bool IsLongAnswer { get; init; }
}
