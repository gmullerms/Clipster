using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;
using AppChatMessage = Clipster.Core.Models.ChatMessage;
using AppChatRole = Clipster.Core.Models.ChatRole;

namespace Clipster.Services.AI;

public class OllamaAiService : IAiService
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _httpClient;
    private TokenUsage? _lastUsage;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaAiService(ISettingsService settings, HttpClient? httpClient = null)
    {
        _settings = settings;
        _httpClient = httpClient ?? new HttpClient();
    }

    public TokenUsage? LastUsage => _lastUsage;

    private string Endpoint => _settings.Settings.OllamaEndpoint.TrimEnd('/');
    private string ModelName => _settings.Settings.OllamaModelName;

    #region Ollama API DTOs

    private class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; set; }
    }

    private class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }

    #endregion

    private static string RoleToString(AppChatRole role) => role switch
    {
        AppChatRole.System => "system",
        AppChatRole.User => "user",
        AppChatRole.Assistant => "assistant",
        _ => "user"
    };

    private List<OllamaMessage> BuildMessages(string systemPrompt, IReadOnlyList<AppChatMessage>? conversationHistory = null)
    {
        var messages = new List<OllamaMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        if (conversationHistory != null)
        {
            foreach (var msg in conversationHistory)
            {
                // Skip system messages since we already added ours
                if (msg.Role == AppChatRole.System) continue;

                messages.Add(new OllamaMessage
                {
                    Role = RoleToString(msg.Role),
                    Content = msg.Content
                });
            }
        }

        return messages;
    }

    private async Task<string> SendChatAsync(OllamaChatRequest request, CancellationToken ct)
    {
        request.Stream = false;
        var url = $"{Endpoint}/api/chat";

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, ct);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, JsonOptions);

        if (result == null)
            throw new InvalidOperationException("Ollama returned an empty response.");

        // Track token usage from the response
        if (result.PromptEvalCount.HasValue || result.EvalCount.HasValue)
        {
            _lastUsage = new TokenUsage
            {
                PromptTokens = result.PromptEvalCount ?? 0,
                CompletionTokens = result.EvalCount ?? 0,
                EstimatedCost = 0m // Ollama is local, no cost
            };
        }

        return result.Message?.Content ?? string.Empty;
    }

    private async IAsyncEnumerable<string> SendChatStreamAsync(
        OllamaChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        request.Stream = true;
        var url = $"{Endpoint}/api/chat";

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var promptTokens = 0;
        var completionTokens = 0;

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            if (chunk == null)
                continue;

            if (chunk.Message?.Content is { Length: > 0 } text)
            {
                yield return text;
            }

            // The final message (done: true) contains token counts
            if (chunk.Done)
            {
                promptTokens = chunk.PromptEvalCount ?? 0;
                completionTokens = chunk.EvalCount ?? 0;
            }
        }

        if (promptTokens > 0 || completionTokens > 0)
        {
            _lastUsage = new TokenUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                EstimatedCost = 0m
            };
        }
    }

    public async Task<string> ChatAsync(IReadOnlyList<AppChatMessage> conversationHistory, CancellationToken ct = default)
    {
        var messages = BuildMessages(PromptTemplates.ClipsterSystemPrompt, conversationHistory);

        // Ensure there's at least one user message
        if (!messages.Any(m => m.Role == "user"))
            messages.Add(new OllamaMessage { Role = "user", Content = "Hello!" });

        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = messages
        };

        return await SendChatAsync(request, ct);
    }

    public IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<AppChatMessage> conversationHistory,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(PromptTemplates.ClipsterSystemPrompt, conversationHistory);

        if (!messages.Any(m => m.Role == "user"))
            messages.Add(new OllamaMessage { Role = "user", Content = "Hello!" });

        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = messages
        };

        return SendChatStreamAsync(request, ct);
    }

    public async Task<string> SummarizeAsync(string text, CancellationToken ct = default)
    {
        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = PromptTemplates.SummarizePrompt },
                new() { Role = "user", Content = text }
            }
        };

        return await SendChatAsync(request, ct);
    }

    public async Task<string> AnalyzeClipboardAsync(ClipboardContent content, string action, CancellationToken ct = default)
    {
        var systemPrompt = string.Format(PromptTemplates.ClipboardAnalysisPrompt, action, content.ContentType);

        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = content.Text }
            }
        };

        return await SendChatAsync(request, ct);
    }

    public async Task<string> AnalyzeScreenAsync(byte[] screenshotPng, string ocrText, CancellationToken ct = default)
    {
        var systemPrompt = string.Format(PromptTemplates.ScreenAnalysisPrompt, ocrText);
        var base64Image = Convert.ToBase64String(screenshotPng);

        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new()
                {
                    Role = "user",
                    Content = "What's on my screen? Please help!",
                    Images = new List<string> { base64Image }
                }
            }
        };

        return await SendChatAsync(request, ct);
    }

    public async Task<string> GenerateTipAsync(string currentContext, CancellationToken ct = default)
    {
        var systemPrompt = string.Format(PromptTemplates.TipGenerationPrompt, currentContext);

        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = "Give me a helpful tip!" }
            }
        };

        return await SendChatAsync(request, ct);
    }

    public async Task<QuickPromptResult> QuickPromptAsync(string prompt, CancellationToken ct = default)
    {
        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = PromptTemplates.QuickPromptSystem },
                new() { Role = "user", Content = prompt }
            }
        };

        var raw = await SendChatAsync(request, ct);
        return ResponseParser.ParseQuickPromptResponse(raw);
    }

    public async Task<ScreenInsight> WatchScreenAsync(byte[] screenshotPng, CancellationToken ct = default)
    {
        var systemPrompt = string.Format(PromptTemplates.ScreenWatcherPrompt, "Manual scan triggered by user.");
        var base64Image = Convert.ToBase64String(screenshotPng);

        var request = new OllamaChatRequest
        {
            Model = ModelName,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new()
                {
                    Role = "user",
                    Content = "What's happening on this screen? Can you help with anything?",
                    Images = new List<string> { base64Image }
                }
            }
        };

        var raw = await SendChatAsync(request, ct);
        return ResponseParser.ParseScreenInsight(raw);
    }
}
