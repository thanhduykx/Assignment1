namespace DataAccessLayer.Entities;

public class Subject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Semester { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Creator { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
    public ICollection<Experiment> Experiments { get; set; } = [];
    public ICollection<TestQuestion> TestQuestions { get; set; } = [];
}
