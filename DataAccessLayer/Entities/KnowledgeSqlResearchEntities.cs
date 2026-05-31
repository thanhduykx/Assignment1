namespace DataAccessLayer.Entities;

internal sealed class KnowledgeSqlResearchEmbeddingModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = "Ollama";
    public string ModelId { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public bool IsActive { get; set; } = true;
    public string ConfigJson { get; set; } = "{}";
}

internal sealed class KnowledgeSqlResearchChunkingStrategy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int ChunkSize { get; set; }
    public int Overlap { get; set; }
    public string Method { get; set; } = "Paragraph";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

internal sealed class KnowledgeSqlResearchFineTunedModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Status { get; set; } = "Ready";
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class KnowledgeSqlResearchExperiment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public List<KnowledgeSqlResearchRun> Runs { get; set; } = new();
    public List<KnowledgeSqlResearchTestQuestion> Questions { get; set; } = new();
}

internal sealed class KnowledgeSqlResearchRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExperimentId { get; set; }
    public string RunName { get; set; } = string.Empty;
    public string RunKind { get; set; } = "Rag";
    public Guid? EmbeddingModelId { get; set; }
    public Guid? ChunkingStrategyId { get; set; }
    public Guid? FineTunedModelId { get; set; }
    public string Status { get; set; } = "Pending";
    public string ParametersJson { get; set; } = "{}";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public KnowledgeSqlResearchExperiment Experiment { get; set; } = null!;
    public KnowledgeSqlResearchEmbeddingModel? EmbeddingModel { get; set; }
    public KnowledgeSqlResearchChunkingStrategy? ChunkingStrategy { get; set; }
    public KnowledgeSqlResearchFineTunedModel? FineTunedModel { get; set; }
    public List<KnowledgeSqlResearchBenchmarkResult> Results { get; set; } = new();
}

internal sealed class KnowledgeSqlResearchTestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExperimentId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public string? Category { get; set; }
    public KnowledgeSqlResearchExperiment Experiment { get; set; } = null!;
}

internal sealed class KnowledgeSqlResearchBenchmarkResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid QuestionId { get; set; }
    public string GeneratedAnswer { get; set; } = string.Empty;
    public double Faithfulness { get; set; }
    public double AnswerRelevancy { get; set; }
    public double ContextPrecision { get; set; }
    public double ContextRecall { get; set; }
    public double RagasScore { get; set; }
    public double LatencyMs { get; set; }
    public string RetrievedChunksJson { get; set; } = "[]";
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
    public KnowledgeSqlResearchRun Run { get; set; } = null!;
    public KnowledgeSqlResearchTestQuestion Question { get; set; } = null!;
}
