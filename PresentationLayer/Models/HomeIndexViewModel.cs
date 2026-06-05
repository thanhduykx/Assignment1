using DataAccessLayer;

namespace PresentationLayer.Models;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<IndexedDocument> Documents { get; set; } = Array.Empty<IndexedDocument>();
    public IReadOnlyList<CourseSubject> CourseCatalog { get; set; } = Array.Empty<CourseSubject>();
    public IReadOnlyList<UserOptionViewModel> LecturerOptions { get; set; } = Array.Empty<UserOptionViewModel>();
    public IReadOnlyList<string> DocumentSubjectOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DocumentChapterOptions { get; set; } = Array.Empty<string>();
    public string? SubjectFilter { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsLecturer { get; set; }
    public int TotalDocumentCount { get; set; }
    public int TotalChunkCount { get; set; }
    public long TotalUploadedBytes { get; set; }
    public int IndexedDocumentCount { get; set; }
    public int ProcessingDocumentCount { get; set; }
    public int FailedDocumentCount { get; set; }
    public double AverageChunksPerIndexedDocument { get; set; }
}

public sealed class ChatIndexViewModel
{
    public IReadOnlyList<ChatSession> ChatSessions { get; set; } = Array.Empty<ChatSession>();
    public IReadOnlyList<IndexedDocument> Documents { get; set; } = Array.Empty<IndexedDocument>();
    public IReadOnlyList<string> SubjectOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ChatQuestionSuggestionViewModel> BenchmarkQuestions { get; set; } = Array.Empty<ChatQuestionSuggestionViewModel>();
}

public sealed class ChatQuestionSuggestionViewModel
{
    public string Question { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}

public sealed class DocumentUploadViewModel
{
    public IFormFile? File { get; set; }
    public string? SourceUrl { get; set; }
    public string? Language { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
}

public sealed class SubjectCatalogViewModel
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
}

public sealed class ChapterCatalogViewModel
{
    public Guid? Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class DocumentEditViewModel
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public int ChunkCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public IReadOnlyList<CourseSubject> CourseCatalog { get; set; } = Array.Empty<CourseSubject>();
}

public sealed class DocumentTextViewModel
{
    public IndexedDocument Document { get; set; } = new();
    public string Content { get; set; } = string.Empty;
}

public sealed class DocumentPreviewViewModel
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public int ChunkCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public string UploadedByEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? IndexedAt { get; set; }
    public string IndexError { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }
    public string ChunkingStrategy { get; set; } = string.Empty;
    public string SubjectOwnerName { get; set; } = string.Empty;
    public string SubjectOwnerEmail { get; set; } = string.Empty;
    public IReadOnlyList<DocumentPreviewChunkViewModel> Chunks { get; set; } = Array.Empty<DocumentPreviewChunkViewModel>();
}

public sealed class DocumentPreviewChunkViewModel
{
    public int ChunkIndex { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public int CharStart { get; set; }
    public int CharEnd { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class ChatRequest
{
    public string? SessionId { get; set; }
    public string? Question { get; set; }
    public string? Language { get; set; }
    public string? SubjectFilter { get; set; }
}

public sealed class ChatSessionRenameRequest
{
    public string? SessionId { get; set; }
    public string? Title { get; set; }
}

public sealed class ChatSessionStarRequest
{
    public string? SessionId { get; set; }
    public bool IsStarred { get; set; }
}

public sealed class ChatSessionDeleteRequest
{
    public string? SessionId { get; set; }
}

public sealed class UserOptionViewModel
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Email : $"{FullName} ({Email})";
}
