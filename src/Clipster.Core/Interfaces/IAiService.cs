using Clipster.Core.Models;

namespace Clipster.Core.Interfaces;

public interface IAiService
{
    Task<string> ChatAsync(IReadOnlyList<ChatMessage> conversationHistory, CancellationToken ct = default);
    Task<string> SummarizeAsync(string text, CancellationToken ct = default);
    Task<string> AnalyzeClipboardAsync(ClipboardContent content, string action, CancellationToken ct = default);
    Task<string> AnalyzeScreenAsync(byte[] screenshotPng, string ocrText, CancellationToken ct = default);
    Task<string> GenerateTipAsync(string currentContext, CancellationToken ct = default);
    Task<QuickPromptResult> QuickPromptAsync(string prompt, CancellationToken ct = default);
    Task<ScreenInsight> WatchScreenAsync(byte[] screenshotPng, CancellationToken ct = default);
}
