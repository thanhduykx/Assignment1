namespace DataAccessLayer.Entities;

internal sealed class KnowledgeSqlDocument
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public int ChunkCount { get; set; }
    public long FileSizeBytes { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public string? UploadedByEmail { get; set; }
    public string Status { get; set; } = DocumentIndexStatus.Indexed;
    public DateTimeOffset? IndexedAt { get; set; }
    public string? IndexError { get; set; }
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }
    public string ChunkingStrategy { get; set; } = string.Empty;
    public ICollection<KnowledgeSqlChunk> Chunks { get; set; } = new List<KnowledgeSqlChunk>();
}
