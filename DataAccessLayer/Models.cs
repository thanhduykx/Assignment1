namespace DataAccessLayer;

public sealed class IndexedDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public int ChunkCount { get; set; }
    public long FileSizeBytes { get; set; }
}

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public Dictionary<int, double> Embedding { get; set; } = new();
}

public sealed class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
}

public sealed class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SourceCitation> Citations { get; set; } = new();
}

public sealed class SourceCitation
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public double Score { get; set; }
    public string Excerpt { get; set; } = string.Empty;
}

public sealed class KnowledgeStore
{
    public List<IndexedDocument> Documents { get; set; } = new();
    public List<DocumentChunk> Chunks { get; set; } = new();
    public List<ChatSession> Sessions { get; set; } = new();
}
