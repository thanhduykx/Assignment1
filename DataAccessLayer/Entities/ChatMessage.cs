using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? RetrievedChunks { get; set; }
    public string? ModelUsed { get; set; }
    public double? ResponseTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public ChatSession Session { get; set; } = null!;
    public ICollection<Citation> Citations { get; set; } = [];
}
