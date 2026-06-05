namespace DataAccessLayer.Entities;

public class ChatSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SubjectId { get; set; }
    public string? SessionName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
