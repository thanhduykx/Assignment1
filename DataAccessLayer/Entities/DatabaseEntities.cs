namespace DataAccessLayer.Entities;

public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "student";
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public ICollection<Subject> CreatedSubjects { get; set; } = new List<Subject>();
    public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<Experiment> Experiments { get; set; } = new List<Experiment>();
    public ICollection<TestQuestion> TestQuestions { get; set; } = new List<TestQuestion>();
}

public sealed class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Semester { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Creator { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<Experiment> Experiments { get; set; } = new List<Experiment>();
    public ICollection<TestQuestion> TestQuestions { get; set; } = new List<TestQuestion>();
}

public sealed class Document
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Chapter { get; set; }
    public string Status { get; set; } = "processing";
    public int UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? IndexedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public User Uploader { get; set; } = null!;
    public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
}

public sealed class Chunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string? ChunkStrategy { get; set; }
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public DateTime CreatedAt { get; set; }

    public Document Document { get; set; } = null!;
    public ChunkingStrategy? ChunkingStrategy { get; set; }
    public ICollection<Embedding> Embeddings { get; set; } = new List<Embedding>();
    public ICollection<Citation> Citations { get; set; } = new List<Citation>();
}

public sealed class Embedding
{
    public int Id { get; set; }
    public int ChunkId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public byte[] VectorData { get; set; } = [];
    public int Dimensions { get; set; }
    public DateTime CreatedAt { get; set; }

    public Chunk Chunk { get; set; } = null!;
    public EmbeddingModel Model { get; set; } = null!;
}

public sealed class EmbeddingModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ModelId { get; set; }
    public int Dimensions { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Config { get; set; }

    public ICollection<Embedding> Embeddings { get; set; } = new List<Embedding>();
    public ICollection<ExperimentRun> ExperimentRuns { get; set; } = new List<ExperimentRun>();
}

public sealed class ChunkingStrategy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ChunkSize { get; set; }
    public int Overlap { get; set; }
    public string Method { get; set; } = "paragraph";
    public string? Description { get; set; }

    public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
    public ICollection<ExperimentRun> ExperimentRuns { get; set; } = new List<ExperimentRun>();
}

public sealed class ChatSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int SubjectId { get; set; }
    public string? SessionName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? RetrievedChunks { get; set; }
    public string? ModelUsed { get; set; }
    public int ResponseTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public ChatSession Session { get; set; } = null!;
    public ICollection<Citation> Citations { get; set; } = new List<Citation>();
}

public sealed class Citation
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public int ChunkId { get; set; }
    public double SimilarityScore { get; set; }
    public int Rank { get; set; }

    public ChatMessage Message { get; set; } = null!;
    public Chunk Chunk { get; set; } = null!;
}

public sealed class Experiment
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Config { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public ICollection<ExperimentRun> Runs { get; set; } = new List<ExperimentRun>();
    public ICollection<FineTuneModel> FineTuneModels { get; set; } = new List<FineTuneModel>();
}

public sealed class ExperimentRun
{
    public int Id { get; set; }
    public int ExperimentId { get; set; }
    public int EmbeddingModelId { get; set; }
    public int ChunkingStrategyId { get; set; }
    public string? RunName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Experiment Experiment { get; set; } = null!;
    public EmbeddingModel EmbeddingModel { get; set; } = null!;
    public ChunkingStrategy ChunkingStrategy { get; set; } = null!;
    public ICollection<BenchmarkResult> BenchmarkResults { get; set; } = new List<BenchmarkResult>();
}

public sealed class TestQuestion
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public ICollection<BenchmarkResult> BenchmarkResults { get; set; } = new List<BenchmarkResult>();
}

public sealed class BenchmarkResult
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public int QuestionId { get; set; }
    public string? GeneratedAnswer { get; set; }
    public double Faithfulness { get; set; }
    public double AnswerRelevancy { get; set; }
    public double ContextPrecision { get; set; }
    public double ContextRecall { get; set; }
    public double RagasScore { get; set; }
    public int LatencyMs { get; set; }
    public DateTime EvaluatedAt { get; set; }

    public ExperimentRun Run { get; set; } = null!;
    public TestQuestion Question { get; set; } = null!;
}

public sealed class FineTuneModel
{
    public int Id { get; set; }
    public int ExperimentId { get; set; }
    public string BaseModel { get; set; } = string.Empty;
    public string? FineTunedModelId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TrainingConfig { get; set; }
    public string? TrainingMetrics { get; set; }
    public DateTime? TrainedAt { get; set; }

    public Experiment Experiment { get; set; } = null!;
}
