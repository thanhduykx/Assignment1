using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public sealed class SqlResearchRepository : IResearchRepository
{
    private readonly DbContextOptions<KnowledgeSqlDbContext> _options;

    public SqlResearchRepository(string connectionString)
    {
        _options = KnowledgeSqlDbContextOptionsFactory.Create(connectionString);
    }

    public async Task<IReadOnlyList<ResearchExperimentSummary>> GetExperimentsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var experiments = await context.ResearchExperiments
            .AsNoTracking()
            .Include(item => item.Runs)
            .ThenInclude(item => item.Results)
            .Include(item => item.Questions)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return experiments.Select(ToSummary).ToList();
    }

    public async Task<ResearchExperimentDetail?> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var experiment = await context.ResearchExperiments
            .AsNoTracking()
            .Include(item => item.Questions)
            .Include(item => item.Runs)
            .ThenInclude(item => item.EmbeddingModel)
            .Include(item => item.Runs)
            .ThenInclude(item => item.ChunkingStrategy)
            .Include(item => item.Runs)
            .ThenInclude(item => item.FineTunedModel)
            .Include(item => item.Runs)
            .ThenInclude(item => item.Results)
            .ThenInclude(item => item.Question)
            .FirstOrDefaultAsync(item => item.Id == experimentId, cancellationToken);

        if (experiment is null)
        {
            return null;
        }

        var summary = ToSummary(experiment);
        return new ResearchExperimentDetail
        {
            Id = summary.Id,
            Name = summary.Name,
            Subject = summary.Subject,
            Status = summary.Status,
            CreatedAt = summary.CreatedAt,
            StartedAt = summary.StartedAt,
            EndedAt = summary.EndedAt,
            RunCount = summary.RunCount,
            QuestionCount = summary.QuestionCount,
            AverageRagasScore = summary.AverageRagasScore,
            ConfigJson = experiment.ConfigJson,
            Questions = experiment.Questions
                .OrderBy(item => item.Question)
                .Select(item => new ResearchTestQuestion
                {
                    Id = item.Id,
                    ExperimentId = item.ExperimentId,
                    Question = item.Question,
                    GroundTruth = item.GroundTruth,
                    Difficulty = item.Difficulty,
                    Category = item.Category
                })
                .ToList(),
            Runs = experiment.Runs
                .OrderBy(item => item.RunKind)
                .ThenBy(item => item.RunName)
                .Select(ToRunSummary)
                .ToList()
        };
    }

    public async Task<IReadOnlyList<ResearchOption>> GetEmbeddingModelsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.ResearchEmbeddingModels
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new ResearchOption
            {
                Id = item.Id,
                Name = item.Name,
                Provider = item.Provider,
                ModelId = item.ModelId,
                IsActive = item.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResearchOption>> GetChunkingStrategiesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.ResearchChunkingStrategies
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new ResearchOption
            {
                Id = item.Id,
                Name = item.Name,
                Provider = item.Method,
                ModelId = $"{item.ChunkSize}/{item.Overlap}",
                Description = item.Description,
                IsActive = item.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateExperimentAsync(CreateResearchExperimentRequest request, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var embeddings = await context.ResearchEmbeddingModels
            .Where(item => request.EmbeddingModelIds.Contains(item.Id)
                && item.IsActive
                && item.Provider == "Gemini")
            .ToListAsync(cancellationToken);
        var strategies = await context.ResearchChunkingStrategies
            .Where(item => request.ChunkingStrategyIds.Contains(item.Id) && item.IsActive)
            .ToListAsync(cancellationToken);

        if (embeddings.Count == 0 || strategies.Count == 0)
        {
            throw new InvalidOperationException("Choose the active Gemini embedding model and at least one active chunking strategy.");
        }

        var experiment = new KnowledgeSqlResearchExperiment
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            Status = "Draft",
            CreatedAt = DateTimeOffset.UtcNow,
            ConfigJson = "{}"
        };

        foreach (var question in request.Questions.Where(item => !string.IsNullOrWhiteSpace(item.Question)))
        {
            experiment.Questions.Add(new KnowledgeSqlResearchTestQuestion
            {
                Id = Guid.NewGuid(),
                ExperimentId = experiment.Id,
                Question = question.Question.Trim(),
                GroundTruth = string.IsNullOrWhiteSpace(question.GroundTruth) ? question.Question.Trim() : question.GroundTruth.Trim(),
                Difficulty = string.IsNullOrWhiteSpace(question.Difficulty) ? "Medium" : question.Difficulty.Trim(),
                Category = question.Category?.Trim()
            });
        }

        if (experiment.Questions.Count == 0)
        {
            throw new InvalidOperationException("Add at least one benchmark question.");
        }

        foreach (var embedding in embeddings)
        {
            foreach (var strategy in strategies)
            {
                experiment.Runs.Add(new KnowledgeSqlResearchRun
                {
                    Id = Guid.NewGuid(),
                    ExperimentId = experiment.Id,
                    RunName = $"{embedding.Name} + {strategy.Name}",
                    RunKind = "Rag",
                    EmbeddingModelId = embedding.Id,
                    ChunkingStrategyId = strategy.Id,
                    Status = "Pending",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(request.FineTunedEndpoint))
        {
            var fineTunedModel = new KnowledgeSqlResearchFineTunedModel
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(request.FineTunedModelName) ? "fine-tuned-baseline" : request.FineTunedModelName.Trim(),
                Endpoint = request.FineTunedEndpoint.Trim(),
                Status = "Ready",
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.ResearchFineTunedModels.Add(fineTunedModel);
            experiment.Runs.Add(new KnowledgeSqlResearchRun
            {
                Id = Guid.NewGuid(),
                ExperimentId = experiment.Id,
                RunName = fineTunedModel.Name,
                RunKind = "FineTuned",
                FineTunedModelId = fineTunedModel.Id,
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        context.ResearchExperiments.Add(experiment);
        await context.SaveChangesAsync(cancellationToken);
        return experiment.Id;
    }

    public async Task<IReadOnlyList<ResearchRunSummary>> GetRunnableRunsAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var runs = await context.ResearchRuns
            .AsNoTracking()
            .Include(item => item.EmbeddingModel)
            .Include(item => item.ChunkingStrategy)
            .Include(item => item.FineTunedModel)
            .Include(item => item.Results)
            .Where(item => item.ExperimentId == experimentId)
            .OrderBy(item => item.RunName)
            .ToListAsync(cancellationToken);

        return runs.Select(ToRunSummary).ToList();
    }

    public async Task SetExperimentStatusAsync(Guid experimentId, string status, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var experiment = await context.ResearchExperiments.FirstOrDefaultAsync(item => item.Id == experimentId, cancellationToken);
        if (experiment is null)
        {
            return;
        }

        experiment.Status = status;
        if (status == "Running")
        {
            experiment.StartedAt ??= DateTimeOffset.UtcNow;
        }
        else if (status is "Completed" or "Failed")
        {
            experiment.EndedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetRunStatusAsync(Guid runId, string status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var run = await context.ResearchRuns.FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Status = status;
        run.ErrorMessage = errorMessage;
        if (status is "Done" or "Error")
        {
            run.CompletedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveBenchmarkResultsAsync(Guid runId, IReadOnlyList<ResearchBenchmarkResult> results, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var existing = context.ResearchBenchmarkResults.Where(item => item.RunId == runId);
        context.ResearchBenchmarkResults.RemoveRange(existing);
        context.ResearchBenchmarkResults.AddRange(results.Select(item => new KnowledgeSqlResearchBenchmarkResult
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            QuestionId = item.QuestionId,
            GeneratedAnswer = item.GeneratedAnswer,
            Faithfulness = item.Faithfulness,
            AnswerRelevancy = item.AnswerRelevancy,
            ContextPrecision = item.ContextPrecision,
            ContextRecall = item.ContextRecall,
            RagasScore = item.RagasScore,
            LatencyMs = item.LatencyMs,
            RetrievedChunksJson = item.RetrievedChunksJson,
            EvaluatedAt = DateTimeOffset.UtcNow
        }));
        await context.SaveChangesAsync(cancellationToken);
    }

    private KnowledgeSqlDbContext CreateContext()
    {
        return new KnowledgeSqlDbContext(_options);
    }

    private static ResearchExperimentSummary ToSummary(KnowledgeSqlResearchExperiment experiment)
    {
        var results = experiment.Runs.SelectMany(item => item.Results).ToList();
        return new ResearchExperimentSummary
        {
            Id = experiment.Id,
            Name = experiment.Name,
            Subject = experiment.Subject,
            Status = experiment.Status,
            CreatedAt = experiment.CreatedAt,
            StartedAt = experiment.StartedAt,
            EndedAt = experiment.EndedAt,
            RunCount = experiment.Runs.Count,
            QuestionCount = experiment.Questions.Count,
            AverageRagasScore = results.Count == 0 ? null : results.Average(item => item.RagasScore)
        };
    }

    private static ResearchRunSummary ToRunSummary(KnowledgeSqlResearchRun run)
    {
        return new ResearchRunSummary
        {
            Id = run.Id,
            ExperimentId = run.ExperimentId,
            RunName = run.RunName,
            RunKind = run.RunKind,
            Status = run.Status,
            EmbeddingModelName = run.EmbeddingModel?.Name,
            EmbeddingProvider = run.EmbeddingModel?.Provider,
            EmbeddingModelIdValue = run.EmbeddingModel?.ModelId,
            EmbeddingConfigJson = run.EmbeddingModel?.ConfigJson,
            ChunkingStrategyName = run.ChunkingStrategy?.Name,
            ChunkingMethod = run.ChunkingStrategy?.Method,
            ChunkSize = run.ChunkingStrategy?.ChunkSize ?? 0,
            ChunkOverlap = run.ChunkingStrategy?.Overlap ?? 0,
            FineTunedModelName = run.FineTunedModel?.Name,
            FineTunedEndpoint = run.FineTunedModel?.Endpoint,
            ErrorMessage = run.ErrorMessage,
            CreatedAt = run.CreatedAt,
            CompletedAt = run.CompletedAt,
            ResultCount = run.Results.Count,
            AverageLatencyMs = Average(run.Results.Select(item => item.LatencyMs)),
            AverageFaithfulness = Average(run.Results.Select(item => item.Faithfulness)),
            AverageAnswerRelevancy = Average(run.Results.Select(item => item.AnswerRelevancy)),
            AverageContextPrecision = Average(run.Results.Select(item => item.ContextPrecision)),
            AverageContextRecall = Average(run.Results.Select(item => item.ContextRecall)),
            AverageRagasScore = Average(run.Results.Select(item => item.RagasScore))
        };
    }

    private static double? Average(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }
}
