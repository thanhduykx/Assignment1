namespace DataAccessLayer.Entities;

internal sealed class KnowledgeSqlChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public KnowledgeSqlChatSession Session { get; set; } = null!;
    public ICollection<KnowledgeSqlCitation> Citations { get; set; } = new List<KnowledgeSqlCitation>();
}
