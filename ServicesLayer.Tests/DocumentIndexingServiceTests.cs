using System.Text;
using DataAccessLayer;
using ServicesLayer;

namespace ServicesLayer.Tests;

public sealed class DocumentIndexingServiceTests
{
    [Fact]
    public async Task QueueAndProcessFile_CreatesIndexedDocumentWithEmbeddedChunks()
    {
        var uploadsRoot = CreateTempUploadsRoot();
        var repository = new InMemoryKnowledgeRepository();
        IEmbeddingService embeddingService = new HashingEmbeddingService();
        var service = new DocumentIndexingService(
            repository,
            new DocumentTextExtractor(),
            embeddingService,
            new ParagraphAwareTextChunker());

        try
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
                Lesson 1:
                Retrieval augmented generation uses indexed chunks and citations.

                Lesson 2:
                Background indexing keeps upload requests fast for lecturers.
                """));

            var result = await service.QueueFileAsync(
                stream,
                "lecture.txt",
                "text/plain",
                "DBA103 - Demo",
                "Chapter 1",
                uploadsRoot,
                new DocumentUploaderInfo(Guid.NewGuid(), "Lecturer", "lecturer@example.edu"));

            var queuedDocument = await repository.GetDocumentAsync(result.DocumentId);
            Assert.NotNull(queuedDocument);
            Assert.Equal(DocumentIndexStatus.Processing, queuedDocument.Status);
            Assert.Equal(0, queuedDocument.ChunkCount);

            await service.ProcessDocumentAsync(result.DocumentId);

            var indexedDocument = await repository.GetDocumentAsync(result.DocumentId);
            var chunks = await repository.GetDocumentChunksAsync(result.DocumentId);

            Assert.NotNull(indexedDocument);
            Assert.Equal(DocumentIndexStatus.Indexed, indexedDocument.Status);
            Assert.Equal(chunks.Count, indexedDocument.ChunkCount);
            Assert.Equal(embeddingService.ModelName, indexedDocument.EmbeddingModel);
            Assert.Equal(embeddingService.Dimensions, indexedDocument.EmbeddingDimensions);
            Assert.Equal("paragraph-aware-950-160", indexedDocument.ChunkingStrategy);
            Assert.NotNull(indexedDocument.IndexedAt);
            Assert.NotEmpty(chunks);
            Assert.All(chunks, chunk =>
            {
                Assert.Equal(indexedDocument.Id, chunk.DocumentId);
                Assert.Equal(indexedDocument.Subject, chunk.Subject);
                Assert.Equal(indexedDocument.Chapter, chunk.Chapter);
                Assert.False(string.IsNullOrWhiteSpace(chunk.Text));
                Assert.NotEmpty(chunk.Embedding);
            });
        }
        finally
        {
            Directory.Delete(uploadsRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueFile_RejectsEmptyFile()
    {
        var uploadsRoot = CreateTempUploadsRoot();
        var repository = new InMemoryKnowledgeRepository();
        var service = new DocumentIndexingService(
            repository,
            new DocumentTextExtractor(),
            new HashingEmbeddingService(),
            new ParagraphAwareTextChunker());

        try
        {
            await using var stream = new MemoryStream();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueFileAsync(
                stream,
                "empty.txt",
                "text/plain",
                "DBA103 - Demo",
                "Chapter 1",
                uploadsRoot,
                new DocumentUploaderInfo(null, null, null)));

            Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await repository.GetDocumentsAsync());
        }
        finally
        {
            Directory.Delete(uploadsRoot, recursive: true);
        }
    }

    private static string CreateTempUploadsRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "assignment1-index-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class InMemoryKnowledgeRepository : IKnowledgeRepository
    {
        private readonly Dictionary<Guid, IndexedDocument> _documents = new();
        private readonly Dictionary<Guid, List<DocumentChunk>> _chunksByDocument = new();

        public Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IndexedDocument>>(_documents.Values.ToList());
        }

        public Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(documentId, out var document);
            return Task.FromResult(document);
        }

        public Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IndexedDocument>>(
                _documents.Values.Where(document => document.Status == status).ToList());
        }

        public Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DocumentChunk>>(
                _chunksByDocument.Values
                    .SelectMany(chunks => chunks)
                    .Where(chunk => _documents.TryGetValue(chunk.DocumentId, out var document)
                                    && document.Status == DocumentIndexStatus.Indexed)
                    .ToList());
        }

        public Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DocumentChunk>>(
                _chunksByDocument.TryGetValue(documentId, out var chunks) ? chunks.ToList() : []);
        }

        public Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
        {
            _documents[document.Id] = document;
            _chunksByDocument[document.Id] = chunks.ToList();
            return Task.CompletedTask;
        }

        public Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var document = GetRequiredDocument(documentId);
            document.Status = DocumentIndexStatus.Processing;
            document.IndexError = string.Empty;
            return Task.CompletedTask;
        }

        public Task CompleteDocumentIndexAsync(
            Guid documentId,
            IReadOnlyList<DocumentChunk> chunks,
            string embeddingModel,
            int embeddingDimensions,
            string chunkingStrategy,
            CancellationToken cancellationToken = default)
        {
            var document = GetRequiredDocument(documentId);
            document.Status = DocumentIndexStatus.Indexed;
            document.ChunkCount = chunks.Count;
            document.IndexedAt = DateTimeOffset.UtcNow;
            document.IndexError = string.Empty;
            document.EmbeddingModel = embeddingModel;
            document.EmbeddingDimensions = embeddingDimensions;
            document.ChunkingStrategy = chunkingStrategy;
            _chunksByDocument[documentId] = chunks.ToList();
            return Task.CompletedTask;
        }

        public Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default)
        {
            var document = GetRequiredDocument(documentId);
            document.Status = DocumentIndexStatus.Failed;
            document.IndexError = errorMessage;
            document.IndexedAt = null;
            return Task.CompletedTask;
        }

        public Task<IndexedDocument> UpdateDocumentMetadataAsync(Guid documentId, string fileName, string subject, string chapter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            _documents.Remove(documentId);
            _chunksByDocument.Remove(documentId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CourseSubject> UpsertSubjectAsync(Guid? subjectId, string code, string name, string? description, CancellationToken cancellationToken = default, SubjectOwnerInfo? ownerInfo = null)
        {
            throw new NotSupportedException();
        }

        public Task DeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CourseChapter> UpsertChapterAsync(Guid? chapterId, Guid subjectId, string title, int sortOrder, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ChatSession?> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null)
        {
            throw new NotSupportedException();
        }

        public Task<ChatSession?> SetSessionStarredAsync(Guid sessionId, bool isStarred, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null)
        {
            throw new NotSupportedException();
        }

        public Task<ChatSession> GetOrCreateSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default,
            ChatSessionOwnerInfo? ownerInfo = null)
        {
            throw new NotSupportedException();
        }

        public Task AddMessageAsync(
            Guid sessionId,
            ChatMessage message,
            CancellationToken cancellationToken = default,
            ChatSessionOwnerInfo? ownerInfo = null)
        {
            throw new NotSupportedException();
        }

        private IndexedDocument GetRequiredDocument(Guid documentId)
        {
            return _documents.TryGetValue(documentId, out var document)
                ? document
                : throw new InvalidOperationException("Document not found.");
        }
    }
}
