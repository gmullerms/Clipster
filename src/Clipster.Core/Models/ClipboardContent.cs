namespace Clipster.Core.Models;

public enum ClipboardContentType
{
    PlainText,
    Code,
    Json,
    Url,
    Email,
    Unknown
}

public class ClipboardContent
{
    public string Text { get; init; } = string.Empty;
    public ClipboardContentType ContentType { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
