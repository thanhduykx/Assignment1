namespace DataAccessLayer.Entities;

internal sealed class KnowledgeSqlChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string EmbeddingJson { get; set; } = string.Empty;
    public KnowledgeSqlDocument Document { get; set; } = null!;
}
