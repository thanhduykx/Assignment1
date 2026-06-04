using System.Text.Json;
using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using DataAccessLayer.Mapping;
using DataAccessLayer.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public sealed class SqlKnowledgeRepository : IKnowledgeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions<KnowledgeSqlDbContext> _options;

    public SqlKnowledgeRepository(string connectionString)
    {
        _options = KnowledgeSqlDbContextOptionsFactory.Create(connectionString);

        using var context = CreateContext();
        KnowledgeSqlSchemaInitializer.EnsureTablesCreated(context);
    }

    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => KnowledgeSqlMapper.ToModel(document))
            .ToListAsync(cancellationToken);
    }

    public async Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var document = await context.Documents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        return document is null ? null : KnowledgeSqlMapper.ToModel(document);
    }

    public async Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeRequiredText(status, "Status is required.");
        await using var context = CreateContext();
        return await context.Documents
            .AsNoTracking()
            .Where(document => document.Status == normalizedStatus)
            .OrderBy(document => document.UploadedAt)
            .Select(document => KnowledgeSqlMapper.ToModel(document))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Chunks
            .AsNoTracking()
            .Where(chunk => chunk.Document.Status == DocumentIndexStatus.Indexed)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => KnowledgeSqlMapper.ToModel(chunk))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Chunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => KnowledgeSqlMapper.ToModel(chunk))
            .ToListAsync(cancellationToken);
    }

    public async Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var existing = await context.Documents.FirstOrDefaultAsync(item => item.Id == document.Id, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        context.Documents.Add(KnowledgeSqlMapper.ToEntity(document));
        context.Chunks.AddRange(chunks.Select(KnowledgeSqlMapper.ToEntity));

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var document = await context.Documents.FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException("Document not found.");

        document.Status = DocumentIndexStatus.Processing;
        document.IndexError = string.Empty;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteDocumentIndexAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        string embeddingModel,
        int embeddingDimensions,
        string chunkingStrategy,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var document = await context.Documents.FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException("Document not found.");

        var existingChunks = await context.Chunks
            .Where(item => item.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        context.Chunks.RemoveRange(existingChunks);
        await context.SaveChangesAsync(cancellationToken);

        document.Status = DocumentIndexStatus.Indexed;
        document.ChunkCount = chunks.Count;
        document.IndexedAt = DateTimeOffset.UtcNow;
        document.IndexError = string.Empty;
        document.EmbeddingModel = embeddingModel.Trim();
        document.EmbeddingDimensions = Math.Max(0, embeddingDimensions);
        document.ChunkingStrategy = chunkingStrategy.Trim();

        context.Chunks.AddRange(chunks.Select(KnowledgeSqlMapper.ToEntity));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var document = await context.Documents.FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        document.Status = DocumentIndexStatus.Failed;
        document.IndexError = (errorMessage ?? string.Empty).Trim();
        document.IndexedAt = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IndexedDocument> UpdateDocumentMetadataAsync(
        Guid documentId,
        string fileName,
        string subject,
        string chapter,
        CancellationToken cancellationToken = default)
    {
        var normalizedFileName = NormalizeFileName(fileName);
        var normalizedSubject = NormalizeRequiredText(subject, "Subject is required.");
        var normalizedChapter = NormalizeRequiredText(chapter, "Chapter is required.");

        await using var context = CreateContext();
        var document = await context.Documents.FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException("Document not found.");

        document.FileName = normalizedFileName;
        document.Subject = normalizedSubject;
        document.Chapter = normalizedChapter;

        var chunks = await context.Chunks
            .Where(item => item.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        foreach (var chunk in chunks)
        {
            chunk.FileName = normalizedFileName;
            chunk.Subject = normalizedSubject;
            chunk.Chapter = normalizedChapter;
        }

        await context.SaveChangesAsync(cancellationToken);
        return KnowledgeSqlMapper.ToModel(document);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var document = await context.Documents.FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        context.Documents.Remove(document);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var subjects = await context.CourseSubjects
            .AsNoTracking()
            .Include(item => item.Chapters)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);

        return subjects.Select(ToCourseSubject).ToList();
    }

    public async Task<CourseSubject> UpsertSubjectAsync(
        Guid? subjectId,
        string code,
        string name,
        string? description,
        CancellationToken cancellationToken = default,
        SubjectOwnerInfo? ownerInfo = null)
    {
        var normalizedCode = NormalizeCode(code);
        var trimmedName = string.IsNullOrWhiteSpace(name) ? normalizedCode : name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new InvalidOperationException("Subject code is required.");
        }

        await using var context = CreateContext();
        var duplicate = await context.CourseSubjects
            .FirstOrDefaultAsync(item => item.Code == normalizedCode && (!subjectId.HasValue || item.Id != subjectId.Value), cancellationToken);
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Subject code already exists.");
        }

        KnowledgeSqlCourseSubject subject;
        if (subjectId.HasValue)
        {
            subject = await context.CourseSubjects.FirstOrDefaultAsync(item => item.Id == subjectId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Subject not found.");
        }
        else
        {
            subject = new KnowledgeSqlCourseSubject
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.CourseSubjects.Add(subject);
        }

        subject.Code = normalizedCode;
        subject.Name = trimmedName;
        subject.Description = description?.Trim() ?? string.Empty;
        if (ownerInfo is not null)
        {
            subject.OwnerUserId = ownerInfo.UserId;
            subject.OwnerName = ownerInfo.Name?.Trim() ?? string.Empty;
            subject.OwnerEmail = ownerInfo.Email?.Trim() ?? string.Empty;
        }

        await context.SaveChangesAsync(cancellationToken);
        await context.Entry(subject).Collection(item => item.Chapters).LoadAsync(cancellationToken);
        return ToCourseSubject(subject);
    }

    public async Task DeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var subject = await context.CourseSubjects.FirstOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (subject is null)
        {
            return;
        }

        context.CourseSubjects.Remove(subject);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CourseChapter> UpsertChapterAsync(
        Guid? chapterId,
        Guid subjectId,
        string title,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var trimmedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            throw new InvalidOperationException("Chapter title is required.");
        }

        await using var context = CreateContext();
        var subject = await context.CourseSubjects.FirstOrDefaultAsync(item => item.Id == subjectId, cancellationToken)
            ?? throw new InvalidOperationException("Subject not found.");
        var duplicate = await context.CourseChapters
            .FirstOrDefaultAsync(item => item.SubjectId == subjectId
                && item.Title == trimmedTitle
                && (!chapterId.HasValue || item.Id != chapterId.Value), cancellationToken);
        if (duplicate is not null)
        {
            throw new InvalidOperationException("Chapter already exists for this subject.");
        }

        KnowledgeSqlCourseChapter chapter;
        if (chapterId.HasValue)
        {
            chapter = await context.CourseChapters.FirstOrDefaultAsync(item => item.Id == chapterId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Chapter not found.");
        }
        else
        {
            chapter = new KnowledgeSqlCourseChapter
            {
                Id = Guid.NewGuid()
            };
            context.CourseChapters.Add(chapter);
        }

        chapter.SubjectId = subjectId;
        chapter.Title = trimmedTitle;
        chapter.SortOrder = sortOrder;
        await context.SaveChangesAsync(cancellationToken);
        chapter.Subject = subject;
        return ToCourseChapter(chapter, subject);
    }

    public async Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var chapter = await context.CourseChapters.FirstOrDefaultAsync(item => item.Id == chapterId, cancellationToken);
        if (chapter is null)
        {
            return;
        }

        context.CourseChapters.Remove(chapter);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var sessions = await context.Sessions
            .AsNoTracking()
            .Include(session => session.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .OrderByDescending(session => session.IsStarred)
            .ThenByDescending(session => session.UpdatedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(KnowledgeSqlMapper.ToModel).ToList();
    }

    public async Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var sessions = await context.Sessions
            .AsNoTracking()
            .Where(session => session.OwnerUserId == ownerUserId)
            .Include(session => session.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .OrderByDescending(session => session.IsStarred)
            .ThenByDescending(session => session.UpdatedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(KnowledgeSqlMapper.ToModel).ToList();
    }

    public async Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var session = await context.Sessions
            .AsNoTracking()
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        return session is null ? null : KnowledgeSqlMapper.ToModel(session);
    }

    public async Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var session = await context.Sessions
            .AsNoTracking()
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.OwnerUserId == ownerUserId, cancellationToken);

        return session is null ? null : KnowledgeSqlMapper.ToModel(session);
    }

    public async Task<ChatSession> GetOrCreateSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await using var context = CreateContext();
        var session = await context.Sessions
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is not null)
        {
            EnsureSessionOwner(session, ownerInfo);
            await context.SaveChangesAsync(cancellationToken);
            return KnowledgeSqlMapper.ToModel(session);
        }

        session = new KnowledgeSqlChatSession
        {
            Id = sessionId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            OwnerUserId = ownerInfo?.UserId,
            OwnerName = ownerInfo?.Name?.Trim() ?? string.Empty,
            OwnerEmail = ownerInfo?.Email?.Trim() ?? string.Empty
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return KnowledgeSqlMapper.ToModel(session);
    }

    public async Task<ChatSession?> RenameSessionAsync(
        Guid sessionId,
        string title,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await using var context = CreateContext();
        var session = await context.Sessions
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        EnsureSessionOwner(session, ownerInfo);
        session.Title = NormalizeSessionTitle(title);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return KnowledgeSqlMapper.ToModel(session);
    }

    public async Task<ChatSession?> SetSessionStarredAsync(
        Guid sessionId,
        bool isStarred,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await using var context = CreateContext();
        var session = await context.Sessions
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        EnsureSessionOwner(session, ownerInfo);
        session.IsStarred = isStarred;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return KnowledgeSqlMapper.ToModel(session);
    }

    public async Task<bool> DeleteSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await using var context = CreateContext();
        var session = await context.Sessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        EnsureSessionOwner(session, ownerInfo);
        context.Sessions.Remove(session);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddMessageAsync(
        Guid sessionId,
        ChatMessage message,
        CancellationToken cancellationToken = default,
        ChatSessionOwnerInfo? ownerInfo = null)
    {
        await using var context = CreateContext();
        var session = await context.Sessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            session = new KnowledgeSqlChatSession
            {
                Id = sessionId,
                Title = IsUserMessage(message) ? BuildSessionTitle(message.Content) : string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                OwnerUserId = ownerInfo?.UserId,
                OwnerName = ownerInfo?.Name?.Trim() ?? string.Empty,
                OwnerEmail = ownerInfo?.Email?.Trim() ?? string.Empty
            };
            context.Sessions.Add(session);
        }
        else
        {
            EnsureSessionOwner(session, ownerInfo);
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(session.Title) && IsUserMessage(message))
        {
            session.Title = BuildSessionTitle(message.Content);
        }

        var messageEntity = KnowledgeSqlMapper.ToEntity(sessionId, message);
        context.Messages.Add(messageEntity);
        context.Citations.AddRange(message.Citations.Select(citation => KnowledgeSqlMapper.ToEntity(messageEntity.Id, citation)));

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ImportFromJsonIfEmptyAsync(string jsonStorePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonStorePath) || !File.Exists(jsonStorePath))
        {
            return;
        }

        await using var context = CreateContext();
        if (await context.Documents.AnyAsync(cancellationToken)
            || await context.Chunks.AnyAsync(cancellationToken)
            || await context.Sessions.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var stream = File.OpenRead(jsonStorePath);
        var store = await JsonSerializer.DeserializeAsync<KnowledgeStore>(stream, JsonOptions, cancellationToken);
        if (store is null)
        {
            return;
        }

        context.Documents.AddRange(store.Documents.Select(KnowledgeSqlMapper.ToEntity));
        context.Chunks.AddRange(store.Chunks.Select(KnowledgeSqlMapper.ToEntity));

        foreach (var session in store.Sessions)
        {
            context.Sessions.Add(new KnowledgeSqlChatSession
            {
                Id = session.Id,
                Title = session.Title,
                IsStarred = session.IsStarred,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                OwnerUserId = session.OwnerUserId,
                OwnerName = session.OwnerName,
                OwnerEmail = session.OwnerEmail
            });

            foreach (var message in session.Messages)
            {
                var messageEntity = KnowledgeSqlMapper.ToEntity(session.Id, message);
                context.Messages.Add(messageEntity);
                context.Citations.AddRange(message.Citations.Select(citation => KnowledgeSqlMapper.ToEntity(messageEntity.Id, citation)));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUserMessage(ChatMessage message)
    {
        return message.Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSessionTitle(string content)
    {
        var normalized = NormalizeSessionTitle(content);
        return normalized.Length <= 56 ? normalized : $"{normalized[..56]}...";
    }

    private static string NormalizeSessionTitle(string title)
    {
        var normalized = string.Join(' ', (title ?? string.Empty)
            .Trim()
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Session title is required.");
        }

        return normalized.Length <= 200 ? normalized : normalized[..200];
    }

    private KnowledgeSqlDbContext CreateContext()
    {
        return new KnowledgeSqlDbContext(_options);
    }

    private static void EnsureSessionOwner(KnowledgeSqlChatSession session, ChatSessionOwnerInfo? ownerInfo)
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

    private static CourseSubject ToCourseSubject(KnowledgeSqlCourseSubject subject)
    {
        return new CourseSubject
        {
            Id = subject.Id,
            Code = subject.Code,
            Name = subject.Name,
            Description = subject.Description,
            CreatedAt = subject.CreatedAt,
            OwnerUserId = subject.OwnerUserId,
            OwnerName = subject.OwnerName ?? string.Empty,
            OwnerEmail = subject.OwnerEmail ?? string.Empty,
            Chapters = subject.Chapters
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Title)
                .Select(item => ToCourseChapter(item, subject))
                .ToList()
        };
    }

    private static CourseChapter ToCourseChapter(KnowledgeSqlCourseChapter chapter, KnowledgeSqlCourseSubject subject)
    {
        return new CourseChapter
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            Title = chapter.Title,
            SortOrder = chapter.SortOrder
        };
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
}
