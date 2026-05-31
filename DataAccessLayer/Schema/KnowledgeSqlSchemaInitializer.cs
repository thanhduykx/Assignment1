using DataAccessLayer.Context;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Schema;

internal static class KnowledgeSqlSchemaInitializer
{
    public static void EnsureTablesCreated(KnowledgeSqlDbContext context)
    {
        context.Database.EnsureCreated();
        EnsureDocumentFileSizeColumn(context);
        BackfillDocumentFileSizes(context);
        SeedResearchCatalog(context);
    }

    private static void EnsureDocumentFileSizeColumn(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_documents', 'FileSizeBytes') IS NULL
            BEGIN
                ALTER TABLE rag_documents ADD FileSizeBytes BIGINT NOT NULL CONSTRAINT DF_rag_documents_FileSizeBytes DEFAULT 0
            END
            """);
    }

    private static void BackfillDocumentFileSizes(KnowledgeSqlDbContext context)
    {
        var changed = false;
        foreach (var document in context.Documents.Where(item => item.FileSizeBytes <= 0).ToList())
        {
            if (!File.Exists(document.StoredPath))
            {
                continue;
            }

            document.FileSizeBytes = new FileInfo(document.StoredPath).Length;
            changed = true;
        }

        if (changed)
        {
            context.SaveChanges();
        }
    }

    private static void SeedResearchCatalog(KnowledgeSqlDbContext context)
    {
        var changed = false;

        foreach (var model in context.ResearchEmbeddingModels.Where(item => item.Provider != "Gemini"))
        {
            if (model.IsActive)
            {
                model.IsActive = false;
                changed = true;
            }
        }

        changed |= UpsertEmbeddingModel(
            context,
            name: "gemini-embedding-001",
            provider: "Gemini",
            modelId: "gemini-embedding-001",
            dimensions: 768,
            configJson: "{\"outputDimensionality\":768}");

        changed |= UpsertEmbeddingModel(
            context,
            name: "gemini-embedding-001-1536d",
            provider: "Gemini",
            modelId: "gemini-embedding-001",
            dimensions: 1536,
            configJson: "{\"outputDimensionality\":1536}");

        changed |= UpsertEmbeddingModel(
            context,
            name: "gemini-embedding-001-3072d",
            provider: "Gemini",
            modelId: "gemini-embedding-001",
            dimensions: 3072,
            configJson: "{\"outputDimensionality\":3072}");

        changed |= AddChunkingStrategyIfMissing(
            context,
            name: "fixed-950",
            chunkSize: 950,
            overlap: 0,
            method: "Fixed",
            description: "Fixed length chunks without overlap.");

        changed |= AddChunkingStrategyIfMissing(
            context,
            name: "sliding-950-160",
            chunkSize: 950,
            overlap: 160,
            method: "SlidingWindow",
            description: "Sliding windows with 160 character overlap.");

        changed |= AddChunkingStrategyIfMissing(
            context,
            name: "paragraph",
            chunkSize: 1200,
            overlap: 120,
            method: "Paragraph",
            description: "Prefer paragraph boundaries.");

        changed |= AddChunkingStrategyIfMissing(
            context,
            name: "semantic-lite",
            chunkSize: 1400,
            overlap: 120,
            method: "SemanticLite",
            description: "Group nearby paragraphs using headings and sentence boundaries.");

        if (changed)
        {
            context.SaveChanges();
        }
    }

    private static bool UpsertEmbeddingModel(
        KnowledgeSqlDbContext context,
        string name,
        string provider,
        string modelId,
        int dimensions,
        string configJson)
    {
        var existing = context.ResearchEmbeddingModels.FirstOrDefault(item => item.Name == name);
        if (existing is not null)
        {
            var changed = false;
            if (existing.Provider != provider)
            {
                existing.Provider = provider;
                changed = true;
            }

            if (existing.ModelId != modelId)
            {
                existing.ModelId = modelId;
                changed = true;
            }

            if (existing.Dimensions != dimensions)
            {
                existing.Dimensions = dimensions;
                changed = true;
            }

            if (existing.ConfigJson != configJson)
            {
                existing.ConfigJson = configJson;
                changed = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                changed = true;
            }

            return changed;
        }

        context.ResearchEmbeddingModels.Add(new KnowledgeSqlResearchEmbeddingModel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Provider = provider,
            ModelId = modelId,
            Dimensions = dimensions,
            IsActive = true,
            ConfigJson = configJson
        });
        return true;
    }

    private static bool AddChunkingStrategyIfMissing(
        KnowledgeSqlDbContext context,
        string name,
        int chunkSize,
        int overlap,
        string method,
        string description)
    {
        if (context.ResearchChunkingStrategies.Any(item => item.Name == name))
        {
            return false;
        }

        context.ResearchChunkingStrategies.Add(new KnowledgeSqlResearchChunkingStrategy
        {
            Id = Guid.NewGuid(),
            Name = name,
            ChunkSize = chunkSize,
            Overlap = overlap,
            Method = method,
            Description = description,
            IsActive = true
        });
        return true;
    }
}
