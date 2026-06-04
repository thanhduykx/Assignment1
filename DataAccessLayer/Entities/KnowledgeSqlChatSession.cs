namespace DataAccessLayer.Entities;

internal sealed class KnowledgeSqlChatSession
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsStarred { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public ICollection<KnowledgeSqlChatMessage> Messages { get; set; } = new List<KnowledgeSqlChatMessage>();
}
