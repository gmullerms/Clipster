using System.Runtime.CompilerServices;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;
using AppChatMessage = Clipster.Core.Models.ChatMessage;
using AppChatRole = Clipster.Core.Models.ChatRole;

namespace Clipster.Services.AI;

public class ClaudeAiService : IAiService
{
    private readonly ISettingsService _settings;
    private TokenUsage? _lastUsage;

    public ClaudeAiService(ISettingsService settings)
    {
        _settings = settings;
    }

    public TokenUsage? LastUsage => _lastUsage;

    private AnthropicClient CreateClient()
    {
        var apiKey = _settings.Settings.ClaudeApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Claude API key is not configured. Please set it in Settings.");

        return new AnthropicClient(apiKey);
    }

    private string ModelName => _settings.Settings.ClaudeModelName;

    private void TrackUsage(MessageResponse response)
    {
        if (response.Usage != null)
        {
            _lastUsage = new TokenUsage
            {
                PromptTokens = response.Usage.InputTokens,
                CompletionTokens = response.Usage.OutputTokens,
                EstimatedCost = EstimateCost(response.Usage.InputTokens, response.Usage.OutputTokens)
            };
        }
    }

    private static decimal EstimateCost(int inputTokens, int outputTokens)
    {
        // Claude Sonnet 4 pricing: $3/M input, $15/M output
        return (inputTokens * 3m / 1_000_000m) + (outputTokens * 15m / 1_000_000m);
    }

    private static List<Message> ConvertMessages(IReadOnlyList<AppChatMessage> conversationHistory)
    {
        var messages = new List<Message>();
        foreach (var msg in conversationHistory)
        {
            // Skip system messages in the messages list; they go in the SystemMessage parameter
            if (msg.Role == AppChatRole.System)
                continue;

            var role = msg.Role switch
            {
                AppChatRole.User => RoleType.User,
                AppChatRole.Assistant => RoleType.Assistant,
                _ => RoleType.User
            };

            messages.Add(new Message(role, msg.Content));
        }
        return messages;
    }

    private static string ExtractText(MessageResponse response)
    {
        // FirstMessage is a TextContent convenience property; fall back to Content list
        if (response.FirstMessage?.Text is { } text)
            return text;

        if (response.Content is { Count: > 0 } && response.Content[0] is TextContent tc)
            return tc.Text;

        return string.Empty;
    }

    public async Task<string> ChatAsync(IReadOnlyList<AppChatMessage> conversationHistory, CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = ConvertMessages(conversationHistory);

        if (messages.Count == 0)
            messages.Add(new Message(RoleType.User, "Hello!"));

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 4096,
            SystemMessage = PromptTemplates.ClipsterSystemPrompt,
            Messages = messages
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        return ExtractText(response);
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<AppChatMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = CreateClient();
        var messages = ConvertMessages(conversationHistory);

        if (messages.Count == 0)
            messages.Add(new Message(RoleType.User, "Hello!"));

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 4096,
            SystemMessage = PromptTemplates.ClipsterSystemPrompt,
            Messages = messages,
            Stream = true
        };

        var totalInput = 0;
        var totalOutput = 0;

        await foreach (var messageResponse in client.Messages.StreamClaudeMessageAsync(parameters, ct))
        {
            if (messageResponse.Delta?.Text is { } text)
            {
                yield return text;
            }

            if (messageResponse.Usage != null)
            {
                if (messageResponse.Usage.InputTokens > 0)
                    totalInput = messageResponse.Usage.InputTokens;
                if (messageResponse.Usage.OutputTokens > 0)
                    totalOutput = messageResponse.Usage.OutputTokens;
            }
        }

        if (totalInput > 0 || totalOutput > 0)
        {
            _lastUsage = new TokenUsage
            {
                PromptTokens = totalInput,
                CompletionTokens = totalOutput,
                EstimatedCost = EstimateCost(totalInput, totalOutput)
            };
        }
    }

    public async Task<string> SummarizeAsync(string text, CancellationToken ct = default)
    {
        var client = CreateClient();
        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 2048,
            SystemMessage = PromptTemplates.SummarizePrompt,
            Messages = new List<Message>
            {
                new(RoleType.User, text)
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        return ExtractText(response);
    }

    public async Task<string> AnalyzeClipboardAsync(ClipboardContent content, string action, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.ClipboardAnalysisPrompt, action, content.ContentType);

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 2048,
            SystemMessage = systemPrompt,
            Messages = new List<Message>
            {
                new(RoleType.User, content.Text)
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        return ExtractText(response);
    }

    public async Task<string> AnalyzeScreenAsync(byte[] screenshotPng, string ocrText, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.ScreenAnalysisPrompt, ocrText);
        var base64Image = Convert.ToBase64String(screenshotPng);

        var userMessage = new Message
        {
            Role = RoleType.User,
            Content = new List<ContentBase>
            {
                new ImageContent
                {
                    Source = new ImageSource
                    {
                        MediaType = "image/png",
                        Data = base64Image
                    }
                },
                new TextContent { Text = "What's on my screen? Please help!" }
            }
        };

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 2048,
            SystemMessage = systemPrompt,
            Messages = new List<Message> { userMessage }
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        return ExtractText(response);
    }

    public async Task<string> GenerateTipAsync(string currentContext, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.TipGenerationPrompt, currentContext);

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 1024,
            SystemMessage = systemPrompt,
            Messages = new List<Message>
            {
                new(RoleType.User, "Give me a helpful tip!")
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        return ExtractText(response);
    }

    public async Task<QuickPromptResult> QuickPromptAsync(string prompt, CancellationToken ct = default)
    {
        var client = CreateClient();

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 2048,
            SystemMessage = PromptTemplates.QuickPromptSystem,
            Messages = new List<Message>
            {
                new(RoleType.User, prompt)
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        var raw = ExtractText(response);

        return ResponseParser.ParseQuickPromptResponse(raw);
    }

    public async Task<ScreenInsight> WatchScreenAsync(byte[] screenshotPng, CancellationToken ct = default)
    {
        var client = CreateClient();
        var systemPrompt = string.Format(PromptTemplates.ScreenWatcherPrompt, "Manual scan triggered by user.");
        var base64Image = Convert.ToBase64String(screenshotPng);

        var userMessage = new Message
        {
            Role = RoleType.User,
            Content = new List<ContentBase>
            {
                new ImageContent
                {
                    Source = new ImageSource
                    {
                        MediaType = "image/png",
                        Data = base64Image
                    }
                },
                new TextContent { Text = "What's happening on this screen? Can you help with anything?" }
            }
        };

        var parameters = new MessageParameters
        {
            Model = ModelName,
            MaxTokens = 2048,
            SystemMessage = systemPrompt,
            Messages = new List<Message> { userMessage }
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ctx: ct);
        TrackUsage(response);
        var raw = ExtractText(response);

        return ResponseParser.ParseScreenInsight(raw);
    }
}
