using DataAccessLayer;
using ServicesLayer;

namespace ServicesLayer.Tests;

public sealed class RagChatServiceTests
{
    [Fact]
    public async Task AskAsync_ReturnsExactCreditFact_WhenNoCreditFieldExists()
    {
        var documentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Syllabus ID: 11835
                    Syllabus Name: Nhac cu truyen thong - Dan Bau
                    Subject Code: DBA103
                    NoCredit: 3
                    Degree Level: Beginner
                    """,
                Embedding = new Dictionary<int, double> { [1] = 1 }
            },
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 3,
                Text = "Cung cap thong tin, tai nguyen len mang va clip nhac truyen thong.",
                Embedding = new Dictionary<int, double> { [2] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 co bao nhieu tin chi?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Equal("DBA103 c\u00f3 3 t\u00edn ch\u1ec9.", answer.Answer);
        Assert.Single(answer.Citations);
        Assert.Equal(1, answer.Citations[0].ChunkIndex);
        Assert.Contains("NoCredit: 3", answer.Citations[0].Excerpt);
    }

    private sealed class NoOpChatCompletionService : ILocalChatCompletionService
    {
        public Task<string> RewriteQuestionAsync(
            string question,
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(question);
        }

        public Task<IReadOnlyList<ChatChunkRerankResult>> RerankChunksAsync(
            string question,
            IReadOnlyList<DocumentChunk> chunks,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatChunkRerankResult>>(Array.Empty<ChatChunkRerankResult>());
        }

        public Task<string?> GenerateAnswerAsync(
            string question,
            string subject,
            IReadOnlyList<ChatMessage> history,
            IReadOnlyList<DocumentChunk> chunks,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class InMemoryChatKnowledgeRepository : IKnowledgeRepository
    {
        private readonly IReadOnlyList<DocumentChunk> _chunks;
        private readonly Dictionary<Guid, ChatSession> _sessions = new();

        public InMemoryChatKnowledgeRepository(IReadOnlyList<DocumentChunk> chunks)
        {
            _chunks = chunks;
        }

        public Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_chunks);
        }

        public Task<ChatSession> GetOrCreateSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default,
            ChatSessionOwnerInfo? ownerInfo = null)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                session = new ChatSession { Id = sessionId };
                _sessions[sessionId] = session;
            }

            return Task.FromResult(session);
        }

        public Task AddMessageAsync(
            Guid sessionId,
            ChatMessage message,
            CancellationToken cancellationToken = default,
            ChatSessionOwnerInfo? ownerInfo = null)
        {
            var session = _sessions.GetValueOrDefault(sessionId) ?? new ChatSession { Id = sessionId };
            session.Messages.Add(message);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            _sessions[sessionId] = session;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteDocumentIndexAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, string embeddingModel, int embeddingDimensions, string chunkingStrategy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IndexedDocument> UpdateDocumentMetadataAsync(Guid documentId, string fileName, string subject, string chapter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CourseSubject> UpsertSubjectAsync(Guid? subjectId, string code, string name, string? description, CancellationToken cancellationToken = default, SubjectOwnerInfo? ownerInfo = null) => throw new NotSupportedException();
        public Task DeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CourseChapter> UpsertChapterAsync(Guid? chapterId, Guid subjectId, string title, int sortOrder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
