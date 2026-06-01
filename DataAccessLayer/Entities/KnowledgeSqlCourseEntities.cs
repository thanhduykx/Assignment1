namespace DataAccessLayer.Entities;

internal sealed class KnowledgeSqlCourseSubject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<KnowledgeSqlCourseChapter> Chapters { get; set; } = new();
}

internal sealed class KnowledgeSqlCourseChapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public KnowledgeSqlCourseSubject Subject { get; set; } = null!;
}
