namespace DataAccessLayer;

public interface IResearchRepository
{
    Task<IReadOnlyList<ResearchExperimentSummary>> GetExperimentsAsync(CancellationToken cancellationToken = default);
    Task<ResearchExperimentDetail?> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResearchOption>> GetEmbeddingModelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResearchOption>> GetChunkingStrategiesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateExperimentAsync(CreateResearchExperimentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResearchRunSummary>> GetRunnableRunsAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task SetExperimentStatusAsync(Guid experimentId, string status, CancellationToken cancellationToken = default);
    Task SetRunStatusAsync(Guid runId, string status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task SaveBenchmarkResultsAsync(Guid runId, IReadOnlyList<ResearchBenchmarkResult> results, CancellationToken cancellationToken = default);
}
