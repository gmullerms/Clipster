namespace Clipster.Core.Models;

public enum InsightType
{
    None,
    Error,
    Warning,
    Suggestion,
    Question
}

public class ScreenInsight
{
    public InsightType Type { get; init; } = InsightType.None;
    public string Summary { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool ShouldNotify { get; init; }
}
