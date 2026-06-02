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
        EnsureDocumentAuditColumns(context);
        EnsureCourseCatalogTables(context);
        EnsureSubjectOwnerColumns(context);
        BackfillDocumentFileSizes(context);
        SeedCourseCatalog(context);
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

    private static void EnsureDocumentAuditColumns(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_documents', 'UploadedByUserId') IS NULL
            BEGIN
                ALTER TABLE rag_documents ADD UploadedByUserId UNIQUEIDENTIFIER NULL
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_documents', 'UploadedByName') IS NULL
            BEGIN
                ALTER TABLE rag_documents ADD UploadedByName NVARCHAR(255) NULL
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_documents', 'UploadedByEmail') IS NULL
            BEGIN
                ALTER TABLE rag_documents ADD UploadedByEmail NVARCHAR(255) NULL
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

    private static void EnsureCourseCatalogTables(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID('rag_subjects', 'U') IS NULL
            BEGIN
                CREATE TABLE rag_subjects (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_rag_subjects PRIMARY KEY,
                    Code NVARCHAR(32) NOT NULL,
                    Name NVARCHAR(255) NOT NULL,
                    Description NVARCHAR(1000) NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL,
                    CONSTRAINT UX_rag_subjects_Code UNIQUE (Code)
                )
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF OBJECT_ID('rag_chapters', 'U') IS NULL
            BEGIN
                CREATE TABLE rag_chapters (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_rag_chapters PRIMARY KEY,
                    SubjectId UNIQUEIDENTIFIER NOT NULL,
                    Title NVARCHAR(255) NOT NULL,
                    SortOrder INT NOT NULL,
                    CONSTRAINT FK_rag_chapters_rag_subjects_SubjectId FOREIGN KEY (SubjectId) REFERENCES rag_subjects(Id) ON DELETE CASCADE,
                    CONSTRAINT UX_rag_chapters_SubjectId_Title UNIQUE (SubjectId, Title)
                )
            END
            """);
    }

    private static void EnsureSubjectOwnerColumns(KnowledgeSqlDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_subjects', 'OwnerUserId') IS NULL
            BEGIN
                ALTER TABLE rag_subjects ADD OwnerUserId UNIQUEIDENTIFIER NULL
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_subjects', 'OwnerName') IS NULL
            BEGIN
                ALTER TABLE rag_subjects ADD OwnerName NVARCHAR(255) NULL
            END
            """);

        context.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('rag_subjects', 'OwnerEmail') IS NULL
            BEGIN
                ALTER TABLE rag_subjects ADD OwnerEmail NVARCHAR(255) NULL
            END
            """);
    }

    private static void SeedCourseCatalog(KnowledgeSqlDbContext context)
    {
        var subject = context.CourseSubjects.FirstOrDefault(item => item.Code == "DBA103");
        if (subject is null)
        {
            subject = new KnowledgeSqlCourseSubject
            {
                Id = Guid.NewGuid(),
                Code = "DBA103",
                Name = "Nhạc cụ truyền thống - Đàn Bầu",
                Description = "Demo subject used for the assignment RAG and RBL benchmark.",
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.CourseSubjects.Add(subject);
            context.SaveChanges();
        }

        if (!context.CourseChapters.Any(item => item.SubjectId == subject.Id && item.Title == "Syllabus 11835"))
        {
            context.CourseChapters.Add(new KnowledgeSqlCourseChapter
            {
                Id = Guid.NewGuid(),
                SubjectId = subject.Id,
                Title = "Syllabus 11835",
                SortOrder = 1
            });
            context.SaveChanges();
        }
    }

    private static void SeedResearchCatalog(KnowledgeSqlDbContext context)
    {
        var changed = false;

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

        changed |= UpsertEmbeddingModel(
            context,
            name: "vinai/phobert-base",
            provider: "HuggingFace",
            modelId: "vinai/phobert-base",
            dimensions: 768,
            configJson: "{\"pooling\":\"mean\",\"normalize\":true,\"task\":\"feature-extraction\"}");

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
