using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Context;

internal sealed class KnowledgeSqlDbContext(DbContextOptions<KnowledgeSqlDbContext> options) : DbContext(options)
{
    public DbSet<KnowledgeSqlDocument> Documents => Set<KnowledgeSqlDocument>();
    public DbSet<KnowledgeSqlChunk> Chunks => Set<KnowledgeSqlChunk>();
    public DbSet<KnowledgeSqlChatSession> Sessions => Set<KnowledgeSqlChatSession>();
    public DbSet<KnowledgeSqlChatMessage> Messages => Set<KnowledgeSqlChatMessage>();
    public DbSet<KnowledgeSqlCitation> Citations => Set<KnowledgeSqlCitation>();
    public DbSet<KnowledgeSqlResearchEmbeddingModel> ResearchEmbeddingModels => Set<KnowledgeSqlResearchEmbeddingModel>();
    public DbSet<KnowledgeSqlResearchChunkingStrategy> ResearchChunkingStrategies => Set<KnowledgeSqlResearchChunkingStrategy>();
    public DbSet<KnowledgeSqlResearchFineTunedModel> ResearchFineTunedModels => Set<KnowledgeSqlResearchFineTunedModel>();
    public DbSet<KnowledgeSqlResearchExperiment> ResearchExperiments => Set<KnowledgeSqlResearchExperiment>();
    public DbSet<KnowledgeSqlResearchRun> ResearchRuns => Set<KnowledgeSqlResearchRun>();
    public DbSet<KnowledgeSqlResearchTestQuestion> ResearchTestQuestions => Set<KnowledgeSqlResearchTestQuestion>();
    public DbSet<KnowledgeSqlResearchBenchmarkResult> ResearchBenchmarkResults => Set<KnowledgeSqlResearchBenchmarkResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnowledgeSqlDocument>(entity =>
        {
            entity.ToTable("rag_documents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.StoredPath).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(255).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.FileSizeBytes).HasDefaultValue(0L);
            entity.HasIndex(item => item.FileName);
        });

        modelBuilder.Entity<KnowledgeSqlChunk>(entity =>
        {
            entity.ToTable("rag_chunks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Text).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.EmbeddingJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => new { item.DocumentId, item.ChunkIndex }).IsUnique();
            entity.HasOne(item => item.Document)
                .WithMany(item => item.Chunks)
                .HasForeignKey(item => item.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlChatSession>(entity =>
        {
            entity.ToTable("rag_chat_sessions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UpdatedAt);
        });

        modelBuilder.Entity<KnowledgeSqlChatMessage>(entity =>
        {
            entity.ToTable("rag_chat_messages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Role).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Content).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => new { item.SessionId, item.CreatedAt });
            entity.HasOne(item => item.Session)
                .WithMany(item => item.Messages)
                .HasForeignKey(item => item.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlCitation>(entity =>
        {
            entity.ToTable("rag_citations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Excerpt).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasOne(item => item.Message)
                .WithMany(item => item.Citations)
                .HasForeignKey(item => item.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlResearchEmbeddingModel>(entity =>
        {
            entity.ToTable("rbl_embedding_models");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Provider).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ModelId).HasMaxLength(255).IsRequired();
            entity.Property(item => item.ConfigJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<KnowledgeSqlResearchChunkingStrategy>(entity =>
        {
            entity.ToTable("rbl_chunking_strategies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Method).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<KnowledgeSqlResearchFineTunedModel>(entity =>
        {
            entity.ToTable("rbl_fine_tuned_models");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Endpoint).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ConfigJson).HasColumnType("nvarchar(max)").IsRequired();
        });

        modelBuilder.Entity<KnowledgeSqlResearchExperiment>(entity =>
        {
            entity.ToTable("rbl_experiments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ConfigJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => item.CreatedAt);
        });

        modelBuilder.Entity<KnowledgeSqlResearchRun>(entity =>
        {
            entity.ToTable("rbl_experiment_runs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RunName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.RunKind).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ParametersJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.HasOne(item => item.Experiment)
                .WithMany(item => item.Runs)
                .HasForeignKey(item => item.ExperimentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.EmbeddingModel)
                .WithMany()
                .HasForeignKey(item => item.EmbeddingModelId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ChunkingStrategy)
                .WithMany()
                .HasForeignKey(item => item.ChunkingStrategyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FineTunedModel)
                .WithMany()
                .HasForeignKey(item => item.FineTunedModelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KnowledgeSqlResearchTestQuestion>(entity =>
        {
            entity.ToTable("rbl_test_questions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Question).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.GroundTruth).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.Difficulty).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(100);
            entity.HasOne(item => item.Experiment)
                .WithMany(item => item.Questions)
                .HasForeignKey(item => item.ExperimentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlResearchBenchmarkResult>(entity =>
        {
            entity.ToTable("rbl_benchmark_results");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.GeneratedAnswer).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.RetrievedChunksJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => new { item.RunId, item.QuestionId }).IsUnique();
            entity.HasOne(item => item.Run)
                .WithMany(item => item.Results)
                .HasForeignKey(item => item.RunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Question)
                .WithMany()
                .HasForeignKey(item => item.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
