namespace DataAccessLayer.Entities;

public class Embedding
{
    public Guid Id { get; set; }
    public Guid ChunkId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public byte[] VectorData { get; set; } = [];
    public int Dimensions { get; set; }
    public DateTime CreatedAt { get; set; }

    public Chunk Chunk { get; set; } = null!;
    public EmbeddingModel Model { get; set; } = null!;
}
