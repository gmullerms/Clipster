using Clipster.Core.Models;

namespace Clipster.Services.AI;

/// <summary>
/// Shared parsing logic for structured AI responses.
/// Used by all provider implementations.
/// </summary>
public static class ResponseParser
{
    public static ScreenInsight ParseScreenInsight(string raw)
    {
        var type = InsightType.None;
        var summary = string.Empty;
        var detail = string.Empty;

        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                var val = trimmed[5..].Trim();
                type = val.ToLowerInvariant() switch
                {
                    "error" => InsightType.Error,
                    "warning" => InsightType.Warning,
                    "suggestion" => InsightType.Suggestion,
                    "question" => InsightType.Question,
                    _ => InsightType.None
                };
            }
            else if (trimmed.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                summary = trimmed[8..].Trim();
            }
            else if (trimmed.StartsWith("DETAIL:", StringComparison.OrdinalIgnoreCase))
            {
                detail = trimmed[7..].Trim();
            }
        }

        return new ScreenInsight
        {
            Type = type,
            Summary = summary,
            Detail = detail,
            ShouldNotify = type != InsightType.None && !string.IsNullOrWhiteSpace(summary)
        };
    }

    public static QuickPromptResult ParseQuickPromptResponse(string raw)
    {
        var separatorIndex = raw.IndexOf("\n---", StringComparison.Ordinal);

        if (separatorIndex >= 0)
        {
            var clipboard = raw[..separatorIndex].Trim();
            var note = raw[(separatorIndex + 4)..].Trim();

            return new QuickPromptResult
            {
                ClipboardContent = clipboard,
                Note = note,
                IsLongAnswer = clipboard.Length > 300 || clipboard.Count(c => c == '\n') > 10
            };
        }

        return new QuickPromptResult
        {
            ClipboardContent = raw.Trim(),
            Note = "Ready to paste!",
            IsLongAnswer = raw.Length > 300
        };
    }
}
