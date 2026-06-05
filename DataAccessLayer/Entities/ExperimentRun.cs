using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class ExperimentRun
{
    public Guid Id { get; set; }
    public Guid ExperimentId { get; set; }
    public Guid EmbeddingModelId { get; set; }
    public Guid ChunkingStrategyId { get; set; }
    public string? RunName { get; set; }
    public ExperimentRunStatus Status { get; set; } = ExperimentRunStatus.Pending;
    public string? Parameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Experiment Experiment { get; set; } = null!;
    public EmbeddingModel EmbeddingModel { get; set; } = null!;
    public ChunkingStrategy ChunkingStrategy { get; set; } = null!;
    public ICollection<BenchmarkResult> BenchmarkResults { get; set; } = [];
}
