namespace DataAccessLayer.Entities;

public class Chunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? TokenCount { get; set; }
    public string? ChunkStrategy { get; set; }
    public int? ChunkSize { get; set; }
    public int? ChunkOverlap { get; set; }
    public DateTime CreatedAt { get; set; }

    public Document Document { get; set; } = null!;
    public ChunkingStrategy? ChunkingStrategy { get; set; }
    public ICollection<Embedding> Embeddings { get; set; } = [];
    public ICollection<Citation> Citations { get; set; } = [];
}
