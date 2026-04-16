using System.Text.Json;
using System.Text.RegularExpressions;
using Clipster.Core.Models;

namespace Clipster.Services.Clipboard;

public partial class ClipboardAnalyzer
{
    public ClipboardContentType Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ClipboardContentType.Unknown;

        var trimmed = text.Trim();

        if (IsJson(trimmed))
            return ClipboardContentType.Json;

        if (IsUrl(trimmed))
            return ClipboardContentType.Url;

        if (IsEmail(trimmed))
            return ClipboardContentType.Email;

        if (IsCode(trimmed))
            return ClipboardContentType.Code;

        return ClipboardContentType.PlainText;
    }

    private static bool IsJson(string text)
    {
        if ((!text.StartsWith('{') || !text.EndsWith('}')) &&
            (!text.StartsWith('[') || !text.EndsWith(']')))
            return false;

        try
        {
            JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUrl(string text)
    {
        // Single line, looks like a URL
        if (text.Contains('\n'))
            return false;

        return UrlRegex().IsMatch(text);
    }

    private static bool IsEmail(string text)
    {
        if (text.Contains('\n'))
            return false;

        return EmailRegex().IsMatch(text);
    }

    private static bool IsCode(string text)
    {
        // Heuristic: code tends to have specific patterns
        var codeSignals = 0;

        if (text.Contains('{') && text.Contains('}')) codeSignals++;
        if (text.Contains(';')) codeSignals++;
        if (text.Contains("=>")) codeSignals++;
        if (text.Contains("//") || text.Contains("/*")) codeSignals++;
        if (text.Contains("function ") || text.Contains("def ") || text.Contains("class ")) codeSignals++;
        if (text.Contains("var ") || text.Contains("let ") || text.Contains("const ")) codeSignals++;
        if (text.Contains("public ") || text.Contains("private ") || text.Contains("static ")) codeSignals++;
        if (text.Contains("import ") || text.Contains("using ") || text.Contains("#include")) codeSignals++;
        if (text.Contains("return ")) codeSignals++;
        if (text.Contains("if (") || text.Contains("for (") || text.Contains("while (")) codeSignals++;
        if (IndentationRegex().IsMatch(text)) codeSignals++;

        return codeSignals >= 2;
    }

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\s{2,}", RegexOptions.Multiline)]
    private static partial Regex IndentationRegex();
}
