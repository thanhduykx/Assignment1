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
    }
}
