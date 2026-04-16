using System.Net.Http;
using Clipster.Core.Interfaces;
using Clipster.Core.Models;

namespace Clipster.Services.AI;

/// <summary>
/// Factory for creating the appropriate IAiService based on the configured LLM provider.
/// </summary>
public static class AiServiceFactory
{
    public static IAiService Create(
        LlmProvider provider,
        ISettingsService settings,
        IHttpClientFactory? httpClientFactory = null)
    {
        return provider switch
        {
            LlmProvider.OpenAI => new OpenAiService(settings),
            LlmProvider.Claude => new ClaudeAiService(settings),
            LlmProvider.Ollama => new OllamaAiService(
                settings,
                httpClientFactory?.CreateClient("Ollama")),
            _ => throw new ArgumentOutOfRangeException(
                nameof(provider),
                provider,
                $"Unsupported LLM provider: {provider}")
        };
    }
}
