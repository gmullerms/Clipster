namespace Clipster.Core.Models;

public enum ChatRole
{
    System,
    User,
    Assistant
}

public class ChatMessage
{
    public ChatRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
