namespace Clipster.Core.Models;

public class Suggestion
{
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string ActionPrompt { get; init; } = string.Empty;
}
