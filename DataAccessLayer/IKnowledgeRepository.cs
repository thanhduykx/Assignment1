namespace DataAccessLayer;

public interface IKnowledgeRepository
{
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task CompleteDocumentIndexAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        string embeddingModel,
        int embeddingDimensions,
        string chunkingStrategy,
        CancellationToken cancellationToken = default);
    Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default);
    Task<IndexedDocument> UpdateDocumentMetadataAsync(Guid documentId, string fileName, string subject, string chapter, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default);
    Task<CourseSubject> UpsertSubjectAsync(Guid? subjectId, string code, string name, string? description, CancellationToken cancellationToken = default, SubjectOwnerInfo? ownerInfo = null);
    Task DeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default);
    Task<CourseChapter> UpsertChapterAsync(Guid? chapterId, Guid subjectId, string title, int sortOrder, CancellationToken cancellationToken = default);
    Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task<ChatSession?> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task<ChatSession?> SetSessionStarredAsync(Guid sessionId, bool isStarred, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
    Task AddMessageAsync(Guid sessionId, ChatMessage message, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null);
}
