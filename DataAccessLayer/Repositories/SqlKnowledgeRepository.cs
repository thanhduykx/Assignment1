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

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Chunks
            .AsNoTracking()
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
        CancellationToken cancellationToken = default)
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
            .OrderByDescending(session => session.UpdatedAt)
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

    public async Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var session = await context.Sessions
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .ThenInclude(message => message.Citations)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is not null)
        {
            return KnowledgeSqlMapper.ToModel(session);
        }

        session = new KnowledgeSqlChatSession
        {
            Id = sessionId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return KnowledgeSqlMapper.ToModel(session);
    }

    public async Task AddMessageAsync(Guid sessionId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var session = await context.Sessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            session = new KnowledgeSqlChatSession
            {
                Id = sessionId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Sessions.Add(session);
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
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
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
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

    private KnowledgeSqlDbContext CreateContext()
    {
        return new KnowledgeSqlDbContext(_options);
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
