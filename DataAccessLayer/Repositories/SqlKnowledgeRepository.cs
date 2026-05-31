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
}
