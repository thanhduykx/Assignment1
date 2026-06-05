namespace DataAccessLayer.Entities;

public sealed class KnowledgeSqlUser
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Provider { get; set; } = "local";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<KnowledgeSqlDocument>? Documents { get; set; }
    public ICollection<KnowledgeSqlChatSession>? Sessions { get; set; }
    public ICollection<KnowledgeSqlCourseSubject>? Subjects { get; set; }
}
