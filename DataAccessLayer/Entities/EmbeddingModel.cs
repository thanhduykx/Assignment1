namespace DataAccessLayer.Entities;

public class EmbeddingModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ModelId { get; set; }
    public int Dimensions { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Config { get; set; }

    public ICollection<Embedding> Embeddings { get; set; } = [];
    public ICollection<ExperimentRun> ExperimentRuns { get; set; } = [];
}
