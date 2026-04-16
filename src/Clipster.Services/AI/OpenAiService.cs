using System.Runtime.CompilerServices;
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

    public TokenUsage? LastUsage { get; private set; }

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

    private static List<AiChatMessage> BuildMessages(string systemPrompt, IReadOnlyList<AppChatMessage> history)
    {
        var messages = new List<AiChatMessage> { AiChatMessage.CreateSystemMessage(systemPrompt) };
        foreach (var msg in history)
        {
            messages.Add(msg.Role switch
            {
                AppChatRole.User => AiChatMessage.CreateUserMessage(msg.Content),
                AppChatRole.Assistant => AiChatMessage.CreateAssistantMessage(msg.Content),
                _ => AiChatMessage.CreateSystemMessage(msg.Content)
            });
        }
        return messages;
    }

    private void TrackUsage(ChatCompletion completion)
    {
        var usage = completion.Usage;
        if (usage == null) return;
        LastUsage = new TokenUsage
        {
            PromptTokens = usage.InputTokenCount,
            CompletionTokens = usage.OutputTokenCount,
            EstimatedCost = EstimateCost(usage.InputTokenCount, usage.OutputTokenCount)
        };
    }

    private decimal EstimateCost(int input, int output)
    {
        var model = _settings.Settings.ModelName.ToLowerInvariant();
        return model switch
        {
            "gpt-4o" => input * 2.5m / 1_000_000 + output * 10m / 1_000_000,
            "gpt-4o-mini" => input * 0.15m / 1_000_000 + output * 0.6m / 1_000_000,
            "gpt-4-turbo" => input * 10m / 1_000_000 + output * 30m / 1_000_000,
            _ => input * 2.5m / 1_000_000 + output * 10m / 1_000_000,
        };
    }

    public async Task<string> ChatAsync(IReadOnlyList<AppChatMessage> conversationHistory, CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = BuildMessages(PromptTemplates.ClipsterSystemPrompt, conversationHistory);
        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        TrackUsage(completion.Value);
        return completion.Value.Content[0].Text;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<AppChatMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = BuildMessages(PromptTemplates.ClipsterSystemPrompt, conversationHistory);

        await foreach (var update in client.CompleteChatStreamingAsync(messages, cancellationToken: ct))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }

            if (update.Usage != null)
            {
                LastUsage = new TokenUsage
                {
                    PromptTokens = update.Usage.InputTokenCount,
                    CompletionTokens = update.Usage.OutputTokenCount,
                    EstimatedCost = EstimateCost(update.Usage.InputTokenCount, update.Usage.OutputTokenCount)
                };
            }
        }
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
        TrackUsage(completion.Value);
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
        TrackUsage(completion.Value);
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
                ChatMessageContentPart.CreateTextPart("What's on my screen? Please help!"))
        };
        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        TrackUsage(completion.Value);
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
        TrackUsage(completion.Value);
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
        TrackUsage(completion.Value);
        return ResponseParser.ParseQuickPromptResponse(completion.Value.Content[0].Text);
    }

    public async Task<ScreenInsight> WatchScreenAsync(byte[] screenshotPng, CancellationToken ct = default)
    {
        return await WatchScreenAsync(screenshotPng, "Manual scan triggered by user.", ct);
    }

    public async Task<ScreenInsight> WatchScreenAsync(byte[] screenshotPng, string context, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.ScreenWatcherPrompt, context);
        var imageData = BinaryData.FromBytes(screenshotPng);
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage(systemPrompt),
            AiChatMessage.CreateUserMessage(
                ChatMessageContentPart.CreateImagePart(imageData, "image/png"),
                ChatMessageContentPart.CreateTextPart("What's happening on this screen? Can you help with anything?"))
        };
        var completion = await client.CompleteChatAsync(messages, cancellationToken: ct);
        TrackUsage(completion.Value);
        return ResponseParser.ParseScreenInsight(completion.Value.Content[0].Text);
    }
}
