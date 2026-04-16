namespace Clipster.Core.Models;

public class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gpt-4o";
    public CharacterStyle CharacterStyle { get; set; } = CharacterStyle.Classic;
    public bool EnableClipboardMonitoring { get; set; } = true;
    public bool EnableProactiveTips { get; set; } = true;
    public bool EnableOcr { get; set; } = true;
    public int TipIntervalMinMinutes { get; set; } = 20;
    public int TipIntervalMaxMinutes { get; set; } = 40;
    public double ClipsterPositionX { get; set; } = double.NaN;
    public double ClipsterPositionY { get; set; } = double.NaN;
}
