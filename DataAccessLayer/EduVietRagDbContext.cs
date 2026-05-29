using DataAccessLayer.Entities;
using EntityChatMessage = DataAccessLayer.Entities.ChatMessage;
using EntityChatSession = DataAccessLayer.Entities.ChatSession;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer;

public class EduVietRagDbContext(DbContextOptions<EduVietRagDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<Embedding> Embeddings => Set<Embedding>();
    public DbSet<EmbeddingModel> EmbeddingModels => Set<EmbeddingModel>();
    public DbSet<ChunkingStrategy> ChunkingStrategies => Set<ChunkingStrategy>();
    public DbSet<EntityChatSession> ChatSessions => Set<EntityChatSession>();
    public DbSet<EntityChatMessage> ChatMessages => Set<EntityChatMessage>();
    public DbSet<Citation> Citations => Set<Citation>();
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<ExperimentRun> ExperimentRuns => Set<ExperimentRun>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<BenchmarkResult> BenchmarkResults => Set<BenchmarkResult>();
    public DbSet<FineTuneModel> FineTuneModels => Set<FineTuneModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.LastLogin).HasColumnName("last_login");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("subjects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("nvarchar(max)");
            entity.Property(x => x.Semester).HasColumnName("semester").HasMaxLength(50);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.Creator).WithMany(x => x.CreatedSubjects).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.SubjectId).HasColumnName("subject_id");
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.FilePath).HasColumnName("file_path").HasMaxLength(1000);
            entity.Property(x => x.FileType).HasColumnName("file_type").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.FileSize).HasColumnName("file_size");
            entity.Property(x => x.Chapter).HasColumnName("chapter").HasMaxLength(100);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(x => x.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.IndexedAt).HasColumnName("indexed_at");
            entity.HasOne(x => x.Subject).WithMany(x => x.Documents).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Uploader).WithMany(x => x.UploadedDocuments).HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.SubjectId);
        });

        modelBuilder.Entity<Chunk>(entity =>
        {
            entity.ToTable("chunks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.DocumentId).HasColumnName("document_id");
            entity.Property(x => x.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(x => x.Content).HasColumnName("content").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.TokenCount).HasColumnName("token_count");
            entity.Property(x => x.ChunkStrategy).HasColumnName("chunk_strategy").HasMaxLength(100);
            entity.Property(x => x.ChunkSize).HasColumnName("chunk_size");
            entity.Property(x => x.ChunkOverlap).HasColumnName("chunk_overlap");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
            entity.HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ChunkingStrategy).WithMany(x => x.Chunks).HasForeignKey(x => x.ChunkStrategy).HasPrincipalKey(x => x.Name).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Embedding>(entity =>
        {
            entity.ToTable("embeddings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.ChunkId).HasColumnName("chunk_id");
            entity.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.VectorData).HasColumnName("vector_data").HasColumnType("varbinary(max)").IsRequired();
            entity.Property(x => x.Dimensions).HasColumnName("dimensions");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.ChunkId, x.ModelName }).IsUnique();
            entity.HasOne(x => x.Chunk).WithMany(x => x.Embeddings).HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Model).WithMany(x => x.Embeddings).HasForeignKey(x => x.ModelName).HasPrincipalKey(x => x.Name).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmbeddingModel>(entity =>
        {
            entity.ToTable("embedding_models", table => table.HasCheckConstraint("CK_embedding_models_config_json", "[config] IS NULL OR ISJSON([config]) = 1"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(100);
            entity.Property(x => x.ModelId).HasColumnName("model_id").HasMaxLength(255);
            entity.Property(x => x.Dimensions).HasColumnName("dimensions");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.Config).HasColumnName("config").HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<ChunkingStrategy>(entity =>
        {
            entity.ToTable("chunking_strategies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ChunkSize).HasColumnName("chunk_size");
            entity.Property(x => x.Overlap).HasColumnName("overlap");
            entity.Property(x => x.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<EntityChatSession>(entity =>
        {
            entity.ToTable("chat_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.SubjectId).HasColumnName("subject_id");
            entity.Property(x => x.SessionName).HasColumnName("session_name").HasMaxLength(255);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.User).WithMany(x => x.ChatSessions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Subject).WithMany(x => x.ChatSessions).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.SubjectId });
        });

        modelBuilder.Entity<EntityChatMessage>(entity =>
        {
            entity.ToTable("chat_messages", table => table.HasCheckConstraint("CK_chat_messages_retrieved_chunks_json", "[retrieved_chunks] IS NULL OR ISJSON([retrieved_chunks]) = 1"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Content).HasColumnName("content").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.RetrievedChunks).HasColumnName("retrieved_chunks").HasColumnType("nvarchar(max)");
            entity.Property(x => x.ModelUsed).HasColumnName("model_used").HasMaxLength(100);
            entity.Property(x => x.ResponseTime).HasColumnName("response_time");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.Session).WithMany(x => x.Messages).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SessionId, x.CreatedAt });
        });

        modelBuilder.Entity<Citation>(entity =>
        {
            entity.ToTable("citations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.MessageId).HasColumnName("message_id");
            entity.Property(x => x.ChunkId).HasColumnName("chunk_id");
            entity.Property(x => x.SimilarityScore).HasColumnName("similarity_score");
            entity.Property(x => x.Rank).HasColumnName("rank");
            entity.HasIndex(x => new { x.MessageId, x.Rank }).IsUnique();
            entity.HasIndex(x => new { x.MessageId, x.ChunkId }).IsUnique();
            entity.HasOne(x => x.Message).WithMany(x => x.Citations).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Chunk).WithMany(x => x.Citations).HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Experiment>(entity =>
        {
            entity.ToTable("experiments", table => table.HasCheckConstraint("CK_experiments_config_json", "[config] IS NULL OR ISJSON([config]) = 1"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.SubjectId).HasColumnName("subject_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.ExperimentType).HasColumnName("experiment_type").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Config).HasColumnName("config").HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");
            entity.HasOne(x => x.Subject).WithMany(x => x.Experiments).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Creator).WithMany(x => x.Experiments).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExperimentRun>(entity =>
        {
            entity.ToTable("experiment_runs", table => table.HasCheckConstraint("CK_experiment_runs_parameters_json", "[parameters] IS NULL OR ISJSON([parameters]) = 1"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.ExperimentId).HasColumnName("experiment_id");
            entity.Property(x => x.EmbeddingModelId).HasColumnName("embedding_model_id");
            entity.Property(x => x.ChunkingStrategyId).HasColumnName("chunking_strategy_id");
            entity.Property(x => x.RunName).HasColumnName("run_name").HasMaxLength(255);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Parameters).HasColumnName("parameters").HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.HasOne(x => x.Experiment).WithMany(x => x.Runs).HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EmbeddingModel).WithMany(x => x.ExperimentRuns).HasForeignKey(x => x.EmbeddingModelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ChunkingStrategy).WithMany(x => x.ExperimentRuns).HasForeignKey(x => x.ChunkingStrategyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TestQuestion>(entity =>
        {
            entity.ToTable("test_questions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.SubjectId).HasColumnName("subject_id");
            entity.Property(x => x.Question).HasColumnName("question").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.GroundTruth).HasColumnName("ground_truth").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Difficulty).HasColumnName("difficulty").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.Subject).WithMany(x => x.TestQuestions).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Creator).WithMany(x => x.TestQuestions).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BenchmarkResult>(entity =>
        {
            entity.ToTable("benchmark_results");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.RunId).HasColumnName("run_id");
            entity.Property(x => x.QuestionId).HasColumnName("question_id");
            entity.Property(x => x.GeneratedAnswer).HasColumnName("generated_answer").HasColumnType("nvarchar(max)");
            entity.Property(x => x.Faithfulness).HasColumnName("faithfulness");
            entity.Property(x => x.AnswerRelevancy).HasColumnName("answer_relevancy");
            entity.Property(x => x.ContextPrecision).HasColumnName("context_precision");
            entity.Property(x => x.ContextRecall).HasColumnName("context_recall");
            entity.Property(x => x.RagasScore).HasColumnName("ragas_score");
            entity.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            entity.Property(x => x.EvaluatedAt).HasColumnName("evaluated_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.RunId, x.QuestionId }).IsUnique();
            entity.HasOne(x => x.Run).WithMany(x => x.BenchmarkResults).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Question).WithMany(x => x.BenchmarkResults).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FineTuneModel>(entity =>
        {
            entity.ToTable("fine_tune_models", table =>
            {
                table.HasCheckConstraint("CK_fine_tune_models_training_config_json", "[training_config] IS NULL OR ISJSON([training_config]) = 1");
                table.HasCheckConstraint("CK_fine_tune_models_training_metrics_json", "[training_metrics] IS NULL OR ISJSON([training_metrics]) = 1");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(x => x.ExperimentId).HasColumnName("experiment_id");
            entity.Property(x => x.BaseModel).HasColumnName("base_model").HasMaxLength(255).IsRequired();
            entity.Property(x => x.FineTunedModelId).HasColumnName("fine_tuned_model_id").HasMaxLength(255);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.TrainingConfig).HasColumnName("training_config").HasColumnType("nvarchar(max)");
            entity.Property(x => x.TrainingMetrics).HasColumnName("training_metrics").HasColumnType("nvarchar(max)");
            entity.Property(x => x.TrainedAt).HasColumnName("trained_at");
            entity.HasOne(x => x.Experiment).WithMany(x => x.FineTuneModels).HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
