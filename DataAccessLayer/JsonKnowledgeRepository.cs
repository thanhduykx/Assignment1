using System.Text.Json;

namespace DataAccessLayer;

public sealed class JsonKnowledgeRepository : IKnowledgeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _storePath;

    public JsonKnowledgeRepository(string storePath)
    {
        _storePath = storePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
    }

    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Documents
                .OrderByDescending(document => document.UploadedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Documents.FirstOrDefault(document => document.Id == documentId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var normalizedStatus = NormalizeRequiredText(status, "Status is required.");
            return (await LoadAsync(cancellationToken)).Documents
                .Where(document => string.Equals(document.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                .OrderBy(document => document.UploadedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var indexedDocumentIds = store.Documents
                .Where(document => string.Equals(document.Status, DocumentIndexStatus.Indexed, StringComparison.OrdinalIgnoreCase))
                .Select(document => document.Id)
                .ToHashSet();
            return store.Chunks
                .Where(chunk => indexedDocumentIds.Contains(chunk.DocumentId))
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Chunks
                .Where(chunk => chunk.DocumentId == documentId)
                .OrderBy(chunk => chunk.ChunkIndex)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            store.Documents.Add(document);
            store.Chunks.AddRange(chunks);
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var document = store.Documents.FirstOrDefault(item => item.Id == documentId)
                ?? throw new InvalidOperationException("Document not found.");

            document.Status = DocumentIndexStatus.Processing;
            document.IndexError = string.Empty;
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteDocumentIndexAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        string embeddingModel,
        int embeddingDimensions,
        string chunkingStrategy,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var document = store.Documents.FirstOrDefault(item => item.Id == documentId)
                ?? throw new InvalidOperationException("Document not found.");

            store.Chunks.RemoveAll(item => item.DocumentId == documentId);
            store.Chunks.AddRange(chunks);

            document.Status = DocumentIndexStatus.Indexed;
            document.ChunkCount = chunks.Count;
            document.IndexedAt = DateTimeOffset.UtcNow;
            document.IndexError = string.Empty;
            document.EmbeddingModel = embeddingModel.Trim();
            document.EmbeddingDimensions = Math.Max(0, embeddingDimensions);
            document.ChunkingStrategy = chunkingStrategy.Trim();

            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var document = store.Documents.FirstOrDefault(item => item.Id == documentId);
            if (document is null)
            {
                return;
            }

            document.Status = DocumentIndexStatus.Failed;
            document.IndexError = (errorMessage ?? string.Empty).Trim();
            document.IndexedAt = null;
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IndexedDocument> UpdateDocumentMetadataAsync(
        Guid documentId,
        string fileName,
        string subject,
        string chapter,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var document = store.Documents.FirstOrDefault(item => item.Id == documentId)
                ?? throw new InvalidOperationException("Document not found.");
            var normalizedFileName = NormalizeFileName(fileName);
            var normalizedSubject = NormalizeRequiredText(subject, "Subject is required.");
            var normalizedChapter = NormalizeRequiredText(chapter, "Chapter is required.");

            document.FileName = normalizedFileName;
            document.Subject = normalizedSubject;
            document.Chapter = normalizedChapter;

            foreach (var chunk in store.Chunks.Where(item => item.DocumentId == documentId))
            {
                chunk.FileName = normalizedFileName;
                chunk.Subject = normalizedSubject;
                chunk.Chapter = normalizedChapter;
            }

            await SaveAsync(store, cancellationToken);
            return document;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            store.Documents.RemoveAll(item => item.Id == documentId);
            store.Chunks.RemoveAll(item => item.DocumentId == documentId);
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).CourseSubjects
                .OrderBy(subject => subject.Code)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseSubject> UpsertSubjectAsync(
        Guid? subjectId,
        string code,
        string name,
        string? description,
        CancellationToken cancellationToken = default,
        SubjectOwnerInfo? ownerInfo = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var normalizedCode = NormalizeCode(code);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                throw new InvalidOperationException("Subject code is required.");
            }

            if (store.CourseSubjects.Any(item => item.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase)
                && (!subjectId.HasValue || item.Id != subjectId.Value)))
            {
                throw new InvalidOperationException("Subject code already exists.");
            }

            var subject = subjectId.HasValue
                ? store.CourseSubjects.FirstOrDefault(item => item.Id == subjectId.Value)
                : null;
            if (subject is null)
            {
                subject = new CourseSubject { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
                store.CourseSubjects.Add(subject);
            }

            subject.Code = normalizedCode;
            subject.Name = string.IsNullOrWhiteSpace(name) ? normalizedCode : name.Trim();
            subject.Description = description?.Trim() ?? string.Empty;
            if (ownerInfo is not null)
            {
                subject.OwnerUserId = ownerInfo.UserId;
                subject.OwnerName = ownerInfo.Name?.Trim() ?? string.Empty;
                subject.OwnerEmail = ownerInfo.Email?.Trim() ?? string.Empty;
            }

            await SaveAsync(store, cancellationToken);
            return subject;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            store.CourseSubjects.RemoveAll(item => item.Id == subjectId);
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CourseChapter> UpsertChapterAsync(
        Guid? chapterId,
        Guid subjectId,
        string title,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var subject = store.CourseSubjects.FirstOrDefault(item => item.Id == subjectId)
                ?? throw new InvalidOperationException("Subject not found.");
            var trimmedTitle = title.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTitle))
            {
                throw new InvalidOperationException("Chapter title is required.");
            }

            if (subject.Chapters.Any(item => item.Title.Equals(trimmedTitle, StringComparison.OrdinalIgnoreCase)
                && (!chapterId.HasValue || item.Id != chapterId.Value)))
            {
                throw new InvalidOperationException("Chapter already exists for this subject.");
            }

            var chapter = chapterId.HasValue
                ? subject.Chapters.FirstOrDefault(item => item.Id == chapterId.Value)
                : null;
            if (chapter is null)
            {
                chapter = new CourseChapter { Id = Guid.NewGuid(), SubjectId = subject.Id };
                subject.Chapters.Add(chapter);
            }

            chapter.SubjectId = subject.Id;
            chapter.SubjectCode = subject.Code;
            chapter.SubjectName = subject.Name;
            chapter.Title = trimmedTitle;
            chapter.SortOrder = sortOrder;
            await SaveAsync(store, cancellationToken);
            return chapter;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            foreach (var subject in store.CourseSubjects)
            {
                subject.Chapters.RemoveAll(item => item.Id == chapterId);
            }

            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Sessions
                .OrderByDescending(session => session.UpdatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Sessions
                .Where(session => session.OwnerUserId == ownerUserId)
                .OrderByDescending(session => session.UpdatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Sessions.FirstOrDefault(item => item.Id == sessionId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Sessions.FirstOrDefault(item => item.Id == sessionId && item.OwnerUserId == ownerUserId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatSession> GetOrCreateSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
            if (session is not null)
            {
                EnsureSessionOwner(session, ownerInfo);
                await SaveAsync(store, cancellationToken);
                return session;
            }

            session = new ChatSession
            {
                Id = sessionId,
                OwnerUserId = ownerInfo?.UserId,
                OwnerName = ownerInfo?.Name?.Trim() ?? string.Empty,
                OwnerEmail = ownerInfo?.Email?.Trim() ?? string.Empty
            };
            store.Sessions.Add(session);
            await SaveAsync(store, cancellationToken);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddMessageAsync(
        Guid sessionId,
        ChatMessage message,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
            if (session is null)
            {
                session = new ChatSession
                {
                    Id = sessionId,
                    OwnerUserId = ownerInfo?.UserId,
                    OwnerName = ownerInfo?.Name?.Trim() ?? string.Empty,
                    OwnerEmail = ownerInfo?.Email?.Trim() ?? string.Empty
                };
                store.Sessions.Add(session);
            }
            else
            {
                EnsureSessionOwner(session, ownerInfo);
            }

            session.Messages.Add(message);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveAsync(store, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<KnowledgeStore> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return new KnowledgeStore();
        }

        await using var stream = File.OpenRead(_storePath);
        return await JsonSerializer.DeserializeAsync<KnowledgeStore>(stream, JsonOptions, cancellationToken) ?? new KnowledgeStore();
    }

    private async Task SaveAsync(KnowledgeStore store, CancellationToken cancellationToken)
    {
        var tempPath = $"{_storePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _storePath, true);
    }

    private static string NormalizeCode(string code)
    {
        return string.Join(string.Empty, (code ?? string.Empty).Trim().ToUpperInvariant().Where(character => !char.IsWhiteSpace(character)));
    }

    private static string NormalizeFileName(string fileName)
    {
        var normalized = Path.GetFileName((fileName ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("File name is required.");
        }

        return normalized;
    }

    private static string NormalizeRequiredText(string value, string errorMessage)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static void EnsureSessionOwner(ChatSession session, ChatSessionOwnerInfo? ownerInfo)
    {
        if (ownerInfo?.UserId is not { } ownerUserId)
        {
            return;
        }

        if (session.OwnerUserId.HasValue && session.OwnerUserId.Value != ownerUserId)
        {
            throw new InvalidOperationException("Chat session is not available for this user.");
        }

        if (!session.OwnerUserId.HasValue)
        {
            session.OwnerUserId = ownerUserId;
            session.OwnerName = ownerInfo.Name?.Trim() ?? string.Empty;
            session.OwnerEmail = ownerInfo.Email?.Trim() ?? string.Empty;
        }
    }
}
