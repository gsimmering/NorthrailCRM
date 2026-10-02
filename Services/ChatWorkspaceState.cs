namespace NorthrailCRM.Services;

public sealed class ChatWorkspaceState
{
    private readonly List<AgentChatConversation> conversations = [];

    public IReadOnlyList<AgentChatConversation> Conversations => conversations;

    public Guid? ActiveConversationId { get; private set; }

    public AgentChatConversation? ActiveConversation => conversations
        .FirstOrDefault(conversation => conversation.Id == ActiveConversationId);

    public event Action? Changed;

    public void EnsureConversation()
    {
        if (ActiveConversation is null)
        {
            StartNewConversation();
        }
    }

    public AgentChatConversation StartNewConversation()
    {
        var conversation = new AgentChatConversation(Guid.NewGuid());
        conversations.Insert(0, conversation);
        ActiveConversationId = conversation.Id;
        Changed?.Invoke();
        return conversation;
    }

    public void SelectConversation(Guid id)
    {
        if (conversations.Any(conversation => conversation.Id == id))
        {
            ActiveConversationId = id;
            Changed?.Invoke();
        }
    }

    public void NotifyConversationChanged(AgentChatConversation conversation)
    {
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        conversations.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));
        Changed?.Invoke();
    }
}

public sealed class AgentChatConversation(Guid id)
{
    public Guid Id { get; } = id;

    public string? FoundryConversationId { get; set; }

    public Task<string>? PendingFoundryConversationCreation { get; set; }

    public string Title { get; set; } = "Neuer Chat";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsSending { get; set; }

    public List<AgentChatMessage> Messages { get; } = [];

    public string Preview => Messages.LastOrDefault()?.Text is { Length: > 0 } text
        ? text
        : "Noch keine Nachrichten";
}

public sealed class AgentChatMessage(string text, bool isUser)
{
    public string Text { get; set; } = text;

    public bool IsUser { get; } = isUser;

    public List<AgentChatAttachment> Attachments { get; } = [];
}

public sealed record AgentChatAttachment(string FileName, string SandboxPath, string DownloadUrl);