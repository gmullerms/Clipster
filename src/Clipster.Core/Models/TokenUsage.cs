namespace Clipster.Core.Models;

public class TokenUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens => PromptTokens + CompletionTokens;
    public decimal EstimatedCost { get; init; }
}
