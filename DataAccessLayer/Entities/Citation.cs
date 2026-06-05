namespace DataAccessLayer.Entities;

public class Citation
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid ChunkId { get; set; }
    public double SimilarityScore { get; set; }
    public int Rank { get; set; }

    public ChatMessage Message { get; set; } = null!;
    public Chunk Chunk { get; set; } = null!;
}
