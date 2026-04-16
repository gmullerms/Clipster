using System.IO;
using System.Text.Json;
using Clipster.Core.Models;

namespace Clipster.Services.Chat;

public class ConversationStore
{
    private static readonly string ConversationsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipster", "conversations");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task SaveConversationAsync(Conversation conversation)
    {
        Directory.CreateDirectory(ConversationsDir);

        // Auto-generate title from first user message if not already set
        if (string.IsNullOrEmpty(conversation.Title))
        {
            var firstUserMessage = conversation.Messages
                .FirstOrDefault(m => m.Role == ChatRole.User);
            if (firstUserMessage is not null)
            {
                var content = firstUserMessage.Content.ReplaceLineEndings(" ").Trim();
                conversation.Title = content.Length > 50 ? content[..50] + "..." : content;
            }
        }

        conversation.UpdatedAt = DateTime.Now;

        var filePath = GetFilePath(conversation.Id);
        var json = JsonSerializer.Serialize(conversation, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<Conversation?> LoadConversationAsync(string id)
    {
        var filePath = GetFilePath(id);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ConversationSummary>> ListConversationsAsync()
    {
        Directory.CreateDirectory(ConversationsDir);

        var summaries = new List<ConversationSummary>();
        var files = Directory.GetFiles(ConversationsDir, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var conversation = JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
                if (conversation is not null)
                {
                    summaries.Add(new ConversationSummary
                    {
                        Id = conversation.Id,
                        Title = conversation.Title,
                        UpdatedAt = conversation.UpdatedAt
                    });
                }
            }
            catch
            {
                // Skip corrupt files
            }
        }

        return summaries
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();
    }

    public Task DeleteConversationAsync(string id)
    {
        var filePath = GetFilePath(id);
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    private static string GetFilePath(string id) => Path.Combine(ConversationsDir, $"{id}.json");
}
