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
}
