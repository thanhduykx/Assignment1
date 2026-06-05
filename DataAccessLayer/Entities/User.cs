using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public ICollection<Subject> CreatedSubjects { get; set; } = [];
    public ICollection<Document> UploadedDocuments { get; set; } = [];
    public ICollection<ChatSession> ChatSessions { get; set; } = [];
    public ICollection<Experiment> Experiments { get; set; } = [];
    public ICollection<TestQuestion> TestQuestions { get; set; } = [];
}
