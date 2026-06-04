using System.Text.Json;
using DataAccessLayer.Entities;

namespace DataAccessLayer.Mapping;

internal static class KnowledgeSqlMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IndexedDocument ToModel(KnowledgeSqlDocument document)
    {
        return new IndexedDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            StoredPath = document.StoredPath,
            Subject = document.Subject,
            Chapter = document.Chapter,
            ContentType = document.ContentType,
            UploadedAt = document.UploadedAt,
            ChunkCount = document.ChunkCount,
            FileSizeBytes = document.FileSizeBytes,
            UploadedByUserId = document.UploadedByUserId,
            UploadedByName = document.UploadedByName ?? string.Empty,
            UploadedByEmail = document.UploadedByEmail ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(document.Status) ? DocumentIndexStatus.Indexed : document.Status,
            IndexedAt = document.IndexedAt,
            IndexError = document.IndexError ?? string.Empty,
            EmbeddingModel = document.EmbeddingModel,
            EmbeddingDimensions = document.EmbeddingDimensions,
            ChunkingStrategy = document.ChunkingStrategy
        };
    }

    public static KnowledgeSqlDocument ToEntity(IndexedDocument document)
    {
        return new KnowledgeSqlDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            StoredPath = document.StoredPath,
            Subject = document.Subject,
            Chapter = document.Chapter,
            ContentType = document.ContentType,
            UploadedAt = document.UploadedAt,
            ChunkCount = document.ChunkCount,
            FileSizeBytes = document.FileSizeBytes,
            UploadedByUserId = document.UploadedByUserId,
            UploadedByName = document.UploadedByName ?? string.Empty,
            UploadedByEmail = document.UploadedByEmail ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(document.Status) ? DocumentIndexStatus.Indexed : document.Status,
            IndexedAt = document.IndexedAt,
            IndexError = document.IndexError ?? string.Empty,
            EmbeddingModel = document.EmbeddingModel,
            EmbeddingDimensions = document.EmbeddingDimensions,
            ChunkingStrategy = document.ChunkingStrategy
        };
    }

    public static DocumentChunk ToModel(KnowledgeSqlChunk chunk)
    {
        return new DocumentChunk
        {
            Id = chunk.Id,
            DocumentId = chunk.DocumentId,
            FileName = chunk.FileName,
            Subject = chunk.Subject,
            Chapter = chunk.Chapter,
            ChunkIndex = chunk.ChunkIndex,
            Text = chunk.Text,
            SectionTitle = chunk.SectionTitle,
            CharStart = chunk.CharStart,
            CharEnd = chunk.CharEnd,
            Embedding = DeserializeEmbedding(chunk.EmbeddingJson)
        };
    }

    public static KnowledgeSqlChunk ToEntity(DocumentChunk chunk)
    {
        return new KnowledgeSqlChunk
        {
            Id = chunk.Id,
            DocumentId = chunk.DocumentId,
            FileName = chunk.FileName,
            Subject = chunk.Subject,
            Chapter = chunk.Chapter,
            ChunkIndex = chunk.ChunkIndex,
            Text = chunk.Text,
            SectionTitle = chunk.SectionTitle,
            CharStart = chunk.CharStart,
            CharEnd = chunk.CharEnd,
            EmbeddingJson = JsonSerializer.Serialize(chunk.Embedding, JsonOptions)
        };
    }

    public static ChatSession ToModel(KnowledgeSqlChatSession session)
    {
        return new ChatSession
        {
            Id = session.Id,
            Title = session.Title ?? string.Empty,
            IsStarred = session.IsStarred,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            OwnerUserId = session.OwnerUserId,
            OwnerName = session.OwnerName ?? string.Empty,
            OwnerEmail = session.OwnerEmail ?? string.Empty,
            Messages = session.Messages
                .OrderBy(message => message.CreatedAt)
                .Select(ToModel)
                .ToList()
        };
    }

    public static KnowledgeSqlChatMessage ToEntity(Guid sessionId, ChatMessage message)
    {
        return new KnowledgeSqlChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = message.Role,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };
    }

    public static KnowledgeSqlCitation ToEntity(Guid messageId, SourceCitation citation)
    {
        return new KnowledgeSqlCitation
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            DocumentId = citation.DocumentId,
            FileName = citation.FileName,
            Subject = citation.Subject,
            Chapter = citation.Chapter,
            ChunkIndex = citation.ChunkIndex,
            Score = citation.Score,
            Excerpt = citation.Excerpt
        };
    }

    private static ChatMessage ToModel(KnowledgeSqlChatMessage message)
    {
        return new ChatMessage
        {
            Role = message.Role,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            Citations = message.Citations
                .OrderBy(citation => citation.ChunkIndex)
                .Select(ToModel)
                .ToList()
        };
    }

    private static SourceCitation ToModel(KnowledgeSqlCitation citation)
    {
        return new SourceCitation
        {
            DocumentId = citation.DocumentId,
            FileName = citation.FileName,
            Subject = citation.Subject,
            Chapter = citation.Chapter,
            ChunkIndex = citation.ChunkIndex,
            Score = citation.Score,
            Excerpt = citation.Excerpt
        };
    }

    private static Dictionary<int, double> DeserializeEmbedding(string embeddingJson)
    {
        return JsonSerializer.Deserialize<Dictionary<int, double>>(embeddingJson, JsonOptions) ?? new Dictionary<int, double>();
    }
}
