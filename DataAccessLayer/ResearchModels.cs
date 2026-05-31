namespace DataAccessLayer;

public sealed class ResearchOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ModelId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ResearchExperimentSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int RunCount { get; set; }
    public int QuestionCount { get; set; }
    public double? AverageRagasScore { get; set; }
}

public sealed class ResearchExperimentDetail : ResearchExperimentSummary
{
    public string? ConfigJson { get; set; }
    public List<ResearchRunSummary> Runs { get; set; } = new();
    public List<ResearchTestQuestion> Questions { get; set; } = new();
}

public sealed class ResearchRunSummary
{
    public Guid Id { get; set; }
    public Guid ExperimentId { get; set; }
    public string RunName { get; set; } = string.Empty;
    public string RunKind { get; set; } = "Rag";
    public string Status { get; set; } = "Pending";
    public string? EmbeddingModelName { get; set; }
    public string? EmbeddingProvider { get; set; }
    public string? EmbeddingModelIdValue { get; set; }
    public string? EmbeddingConfigJson { get; set; }
    public string? ChunkingStrategyName { get; set; }
    public string? ChunkingMethod { get; set; }
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public string? FineTunedModelName { get; set; }
    public string? FineTunedEndpoint { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int ResultCount { get; set; }
    public double? AverageLatencyMs { get; set; }
    public double? AverageFaithfulness { get; set; }
    public double? AverageAnswerRelevancy { get; set; }
    public double? AverageContextPrecision { get; set; }
    public double? AverageContextRecall { get; set; }
    public double? AverageRagasScore { get; set; }
}

public sealed class ResearchBenchmarkResult
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid QuestionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string GeneratedAnswer { get; set; } = string.Empty;
    public double Faithfulness { get; set; }
    public double AnswerRelevancy { get; set; }
    public double ContextPrecision { get; set; }
    public double ContextRecall { get; set; }
    public double RagasScore { get; set; }
    public double LatencyMs { get; set; }
    public string RetrievedChunksJson { get; set; } = "[]";
    public DateTimeOffset EvaluatedAt { get; set; }
}

public sealed class ResearchTestQuestion
{
    public Guid Id { get; set; }
    public Guid ExperimentId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public string? Category { get; set; }
}

public sealed class CreateResearchExperimentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public IReadOnlyList<Guid> EmbeddingModelIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> ChunkingStrategyIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<ResearchQuestionInput> Questions { get; set; } = Array.Empty<ResearchQuestionInput>();
    public string? FineTunedModelName { get; set; }
    public string? FineTunedEndpoint { get; set; }
}

public sealed class ResearchQuestionInput
{
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public string? Category { get; set; }
}
