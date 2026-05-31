using DataAccessLayer.Context;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Schema;

internal static class KnowledgeSqlSchemaInitializer
{
    public static void EnsureTablesCreated(KnowledgeSqlDbContext context)
    {
        context.Database.EnsureCreated();
        SeedResearchCatalog(context);
    }

    private static void SeedResearchCatalog(KnowledgeSqlDbContext context)
    {
        var changed = false;

        changed |= AddEmbeddingModelIfMissing(
            context,
            name: "hashing-baseline",
            provider: "Hashing",
            modelId: "hashing-baseline",
            dimensions: 512,
            configJson: "{}");

        changed |= AddEmbeddingModelIfMissing(
            context,
            name: "bge-m3",
            provider: "Ollama",
            modelId: "bge-m3",
            dimensions: 1024,
            configJson: "{\"baseUrl\":\"http://localhost:11434\"}");

        changed |= AddEmbeddingModelIfMissing(
            context,
            name: "multilingual-e5-base",
            provider: "Ollama",
            modelId: "multilingual-e5-base",
            dimensions: 768,
            configJson: "{\"baseUrl\":\"http://localhost:11434\"}");

        changed |= AddEmbeddingModelIfMissing(
            context,
            name: "phobert-base",
            provider: "Ollama",
            modelId: "phobert-base",
            dimensions: 768,
            configJson: "{\"baseUrl\":\"http://localhost:11434\"}");

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

    private static bool AddEmbeddingModelIfMissing(
        KnowledgeSqlDbContext context,
        string name,
        string provider,
        string modelId,
        int dimensions,
        string configJson)
    {
        if (context.ResearchEmbeddingModels.Any(item => item.Name == name))
        {
            return false;
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
