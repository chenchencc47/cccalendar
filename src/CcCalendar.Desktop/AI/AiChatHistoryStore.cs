using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CcCalendar.Desktop.AI;

/// <summary>
/// AI 聊天多会话本地持久化：每个会话独立标题与消息列表，
/// JSON 文件保存全部会话，重启后恢复；自动迁移旧版单会话文件。
/// </summary>
public sealed class AiChatHistoryStore
{
    public const int MaximumEntries = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string filePath;
    private readonly string? legacyFilePath;

    public AiChatHistoryStore(string? filePath = null, string? legacyFilePath = null)
    {
        this.filePath = filePath ?? Path.Combine(
            ApplicationPaths.RootDirectory,
            "ai-chat-sessions.json");
        this.legacyFilePath = legacyFilePath ?? Path.Combine(
            ApplicationPaths.RootDirectory,
            "ai-chat-history.json");
    }

    public ObservableCollection<AssistantChatSession> Load()
    {
        ObservableCollection<AssistantChatSession> sessions = [.. ReadSessions()];
        if (sessions.Count == 0
            && File.Exists(legacyFilePath)
            && ReadLegacyMessages() is { Count: > 0 } legacy)
        {
            sessions.Add(new AssistantChatSession(
                Guid.NewGuid(),
                "历史对话",
                DateTimeOffset.UtcNow,
                legacy));
            TryWriteFile(sessions);
            TryDeleteFile(legacyFilePath);
        }

        return sessions;
    }

    public void Save(ObservableCollection<AssistantChatSession> sessions)
    {
        // 空会话不落盘：避免"新建未使用"的会话堆积。
        ObservableCollection<AssistantChatSession> persistable = [..
            sessions.Where(session => session.Messages.Count > 0)];
        TryWriteFile(persistable);
    }

    public void Delete(AssistantChatSession session)
    {
        ObservableCollection<AssistantChatSession> remaining = [..
            ReadSessions().Where(item => item.Id != session.Id)];
        TryWriteFile(remaining);
    }

    private List<AssistantChatSession> ReadSessions()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            string json = File.ReadAllText(filePath);
            StoredChatSession[]? stored = JsonSerializer.Deserialize<StoredChatSession[]>(
                json,
                SerializerOptions);
            return stored is null
                ? []
                : [.. stored.Select(item => new AssistantChatSession(
                    item.Id,
                    item.Title,
                    item.CreatedAtUtc,
                    [.. (item.Messages ?? [])
                        .TakeLast(MaximumEntries)
                        .Select(message => new StoredChatMessage(
                            message.IsUser,
                            message.Text,
                            message.CreatedAtUtc,
                            message.ThinkingText))]))];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // 历史损坏不应阻断应用启动。
            return [];
        }
    }

    private List<StoredChatMessage> ReadLegacyMessages()
    {
        try
        {
            string json = File.ReadAllText(legacyFilePath!);
            StoredChatMessage[]? entries = JsonSerializer.Deserialize<StoredChatMessage[]>(
                json,
                SerializerOptions);
            return entries is { Length: > 0 }
                ? [.. entries.TakeLast(MaximumEntries)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void TryWriteFile(IEnumerable<AssistantChatSession> sessions)
    {
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StoredChatSession[] stored = [.. sessions.Select(session => new StoredChatSession(
                session.Id,
                session.Title,
                session.CreatedAtUtc,
                [.. session.Messages.TakeLast(MaximumEntries)]))];
            string temporaryPath = $"{filePath}.tmp-{Guid.NewGuid():N}";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(stored, SerializerOptions));
                File.Move(temporaryPath, filePath, overwrite: true);
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 保存失败不影响聊天流程。
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 旧文件删除失败可忽略。
        }
    }
}

public sealed record StoredChatMessage(
    [property: JsonPropertyName("isUser")] bool IsUser,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("at")] DateTimeOffset? CreatedAtUtc,
    [property: JsonPropertyName("thinking")] string? ThinkingText = null);

public sealed record StoredChatSession(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAtUtc,
    [property: JsonPropertyName("messages")] List<StoredChatMessage> Messages);

/// <summary>一个聊天会话：标题 + 消息列表，标题取自首条用户消息。</summary>
public sealed class AssistantChatSession : INotifyPropertyChanged
{
    private string title;

    public AssistantChatSession(
        Guid id,
        string title,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<StoredChatMessage> messages)
    {
        Id = id;
        this.title = title;
        CreatedAtUtc = createdAtUtc;
        Messages = [.. messages];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string Title
    {
        get => title;
        set
        {
            if (title != value)
            {
                title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
    }

    public ObservableCollection<StoredChatMessage> Messages { get; }
}
