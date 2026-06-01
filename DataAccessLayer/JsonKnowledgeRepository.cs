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

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadAsync(cancellationToken)).Chunks.ToList();
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
        CancellationToken cancellationToken = default)
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

    public async Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
            if (session is not null)
            {
                return session;
            }

            session = new ChatSession { Id = sessionId };
            store.Sessions.Add(session);
            await SaveAsync(store, cancellationToken);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddMessageAsync(Guid sessionId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var session = store.Sessions.FirstOrDefault(item => item.Id == sessionId);
            if (session is null)
            {
                session = new ChatSession { Id = sessionId };
                store.Sessions.Add(session);
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
}
