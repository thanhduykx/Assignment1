using DataAccessLayer;

namespace PresentationLayer.Models;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<IndexedDocument> Documents { get; set; } = Array.Empty<IndexedDocument>();
    public IReadOnlyList<CourseSubject> CourseCatalog { get; set; } = Array.Empty<CourseSubject>();
}

public sealed class ChatIndexViewModel
{
    public IReadOnlyList<ChatSession> ChatSessions { get; set; } = Array.Empty<ChatSession>();
    public IReadOnlyList<IndexedDocument> Documents { get; set; } = Array.Empty<IndexedDocument>();
}

public sealed class DocumentUploadViewModel
{
    public IFormFile? File { get; set; }
    public string? SourceUrl { get; set; }
    public string? Language { get; set; }
    public string Subject { get; set; } = "DBA103 - Traditional musical instrument";
    public string Chapter { get; set; } = "Syllabus 11835";
}

public sealed class SubjectCatalogViewModel
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class ChapterCatalogViewModel
{
    public Guid? Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class DocumentTextViewModel
{
    public IndexedDocument Document { get; set; } = new();
    public string Content { get; set; } = string.Empty;
}

public sealed class ChatRequest
{
    public string? SessionId { get; set; }
    public string? Question { get; set; }
    public string? Language { get; set; }
}
