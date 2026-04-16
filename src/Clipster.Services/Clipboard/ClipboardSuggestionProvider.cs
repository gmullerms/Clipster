using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.Clipboard;

public static class ClipboardSuggestionProvider
{
    public static (string message, List<BubbleAction> actions) GetSuggestions(
        ClipboardContent content,
        Action<string> openChatWithPrompt)
    {
        var preview = content.Text.Length > 60
            ? content.Text[..57] + "..."
            : content.Text;

        return content.ContentType switch
        {
            ClipboardContentType.Code => (
                $"I see you copied some code!\n\"{preview}\"",
                new List<BubbleAction>
                {
                    new() { Label = "Explain", Callback = () => openChatWithPrompt($"Please explain this code:\n\n```\n{content.Text}\n```") },
                    new() { Label = "Improve", Callback = () => openChatWithPrompt($"Please review and suggest improvements for this code:\n\n```\n{content.Text}\n```") },
                }),

            ClipboardContentType.Json => (
                $"Looks like JSON data!\n\"{preview}\"",
                new List<BubbleAction>
                {
                    new() { Label = "Analyze", Callback = () => openChatWithPrompt($"Please analyze this JSON data and explain its structure:\n\n```json\n{content.Text}\n```") },
                    new() { Label = "Format", Callback = () => openChatWithPrompt($"Please pretty-print and format this JSON:\n\n```json\n{content.Text}\n```") },
                }),

            ClipboardContentType.Url => (
                $"Got a link!\n{preview}",
                new List<BubbleAction>
                {
                    new() { Label = "What is this?", Callback = () => openChatWithPrompt($"What can you tell me about this URL? {content.Text}") },
                }),

            ClipboardContentType.Email => (
                $"That looks like an email address!",
                new List<BubbleAction>
                {
                    new() { Label = "Draft email", Callback = () => openChatWithPrompt($"Help me draft a professional email to {content.Text}") },
                }),

            ClipboardContentType.PlainText => (
                content.Text.Length > 100
                    ? $"That's a lot of text! ({content.Text.Length} chars)\n\"{preview}\""
                    : $"I see you copied:\n\"{preview}\"",
                new List<BubbleAction>
                {
                    new() { Label = "Summarize", Callback = () => openChatWithPrompt($"Please summarize this text:\n\n{content.Text}") },
                    new() { Label = "Rewrite", Callback = () => openChatWithPrompt($"Please rewrite this text to be clearer and more concise:\n\n{content.Text}") },
                }),

            _ => (
                $"You copied something!\n\"{preview}\"",
                new List<BubbleAction>
                {
                    new() { Label = "Help with this", Callback = () => openChatWithPrompt(content.Text) },
                }),
        };
    }
}
