namespace Clipster.Core.Models;

public class AppSettings
{
    // LLM Provider
    public LlmProvider Provider { get; set; } = LlmProvider.OpenAI;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gpt-4o";

    // Claude-specific
    public string ClaudeApiKey { get; set; } = string.Empty;
    public string ClaudeModelName { get; set; } = "claude-sonnet-4-20250514";

    // Ollama-specific
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModelName { get; set; } = "llama3";

    // Character
    public CharacterStyle CharacterStyle { get; set; } = CharacterStyle.Classic;

    // Features
    public bool EnableClipboardMonitoring { get; set; } = true;
    public bool EnableProactiveTips { get; set; } = true;
    public bool EnableOcr { get; set; } = true;
    public bool EnableScreenWatcher { get; set; } = true;

    // Tip intervals
    public int TipIntervalMinMinutes { get; set; } = 20;
    public int TipIntervalMaxMinutes { get; set; } = 40;

    // Screen watcher
    public int ScreenWatchIntervalMinutes { get; set; } = 5;
    public int IdleThresholdSeconds { get; set; } = 120;

    // Custom commands
    public List<CustomCommand> CustomCommands { get; set; } = new();

    // Position
    public double ClipsterPositionX { get; set; } = double.NaN;
    public double ClipsterPositionY { get; set; } = double.NaN;
}
