using Clipster.Core.Interfaces;
using Clipster.Core.Models;
using OpenAI;
using OpenAI.Chat;
using AiChatMessage = OpenAI.Chat.ChatMessage;
using AppChatMessage = Clipster.Core.Models.ChatMessage;
using AppChatRole = Clipster.Core.Models.ChatRole;

namespace Clipster.Services.AI;

public class OpenAiService : IAiService
{
    private readonly ISettingsService _settings;

    public OpenAiService(ISettingsService settings)
    {
        _settings = settings;
    }

    private ChatClient CreateClient()
    {
        var apiKey = _settings.Settings.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured. Please set it in Settings.");

        var client = new OpenAIClient(apiKey);
        return client.GetChatClient(_settings.Settings.ModelName);
    }

    public async Task<string> ChatAsync(IReadOnlyList<AppChatMessage> conversationHistory, CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(PromptTemplates.ClipsterSystemPrompt)
        };

        foreach (var msg in conversationHistory)
        {
            messages.Add(msg.Role switch
            {
                AppChatRole.User => AiChatMessage.CreateUserMessage(msg.Content),
                AppChatRole.Assistant => AiChatMessage.CreateAssistantMessage(msg.Content),
                _ => AiChatMessage.CreateSystemMessage(msg.Content)
            });
        }

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return completion.Value.Content[0].Text;
    }

    public async Task<string> SummarizeAsync(string text, CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(PromptTemplates.SummarizePrompt),
            AiChatMessage.CreateUserMessage(text)
        };

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return completion.Value.Content[0].Text;
    }

    public async Task<string> AnalyzeClipboardAsync(ClipboardContent content, string action, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.ClipboardAnalysisPrompt, action, content.ContentType);
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(systemPrompt),
            AiChatMessage.CreateUserMessage(content.Text)
        };

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return completion.Value.Content[0].Text;
    }

    public async Task<string> AnalyzeScreenAsync(byte[] screenshotPng, string ocrText, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.ScreenAnalysisPrompt, ocrText);

        var imageData = BinaryData.FromBytes(screenshotPng);
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(systemPrompt),
            AiChatMessage.CreateUserMessage(
                ChatMessageContentPart.CreateImagePart(imageData, "image/png"),
                ChatMessageContentPart.CreateTextPart("What's on my screen? Please help!")
            )
        };

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return completion.Value.Content[0].Text;
    }

    public async Task<string> GenerateTipAsync(string currentContext, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.TipGenerationPrompt, currentContext);
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(systemPrompt),
            AiChatMessage.CreateUserMessage("Give me a helpful tip!")
        };

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        return completion.Value.Content[0].Text;
    }

    public async Task<QuickPromptResult> QuickPromptAsync(string prompt, CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(PromptTemplates.QuickPromptSystem),
            AiChatMessage.CreateUserMessage(prompt)
        };

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        var raw = completion.Value.Content[0].Text;

        return ParseQuickPromptResponse(raw);
    }

    public async Task<ScreenInsight> WatchScreenAsync(byte[] screenshotPng, CancellationToken ct = default)
    {
        var client = CreateClient();
        var imageData = BinaryData.FromBytes(screenshotPng);
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(PromptTemplates.ScreenWatcherPrompt),
            AiChatMessage.CreateUserMessage(
                ChatMessageContentPart.CreateImagePart(imageData, "image/png"),
                ChatMessageContentPart.CreateTextPart("Analyze this screenshot. What do you see?"))
        };

        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        var raw = completion.Value.Content[0].Text;

        return ParseScreenInsight(raw);
    }

    private static ScreenInsight ParseScreenInsight(string raw)
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

    private static QuickPromptResult ParseQuickPromptResponse(string raw)
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
