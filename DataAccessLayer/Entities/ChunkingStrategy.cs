using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class ChunkingStrategy
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ChunkSize { get; set; }
    public int Overlap { get; set; }
    public ChunkingMethod Method { get; set; }
    public string? Description { get; set; }

    public ICollection<Chunk> Chunks { get; set; } = [];
    public ICollection<ExperimentRun> ExperimentRuns { get; set; } = [];
}
