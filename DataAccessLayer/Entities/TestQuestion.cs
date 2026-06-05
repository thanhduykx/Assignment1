using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class TestQuestion
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public QuestionDifficulty Difficulty { get; set; }
    public string? Category { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public ICollection<BenchmarkResult> BenchmarkResults { get; set; } = [];
}
