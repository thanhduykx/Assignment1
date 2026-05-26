namespace DataAccessLayer.Entities;

public class BenchmarkResult
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid QuestionId { get; set; }
    public string? GeneratedAnswer { get; set; }
    public double? Faithfulness { get; set; }
    public double? AnswerRelevancy { get; set; }
    public double? ContextPrecision { get; set; }
    public double? ContextRecall { get; set; }
    public double? RagasScore { get; set; }
    public double? LatencyMs { get; set; }
    public DateTime EvaluatedAt { get; set; }

    public ExperimentRun Run { get; set; } = null!;
    public TestQuestion Question { get; set; } = null!;
}
