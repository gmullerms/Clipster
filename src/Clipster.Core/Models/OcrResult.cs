namespace Clipster.Core.Models;

public class OcrResult
{
    public string Text { get; init; } = string.Empty;
    public float Confidence { get; init; }
}
