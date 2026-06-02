using System.Text;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record DocumentUploadResult(Guid DocumentId, int ChunkCount, string Message);

public sealed record DocumentUploaderInfo(Guid? UserId, string? Name, string? Email);

public interface IDocumentIndexingService
{
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<DocumentUploadResult> IndexAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default);
    Task<DocumentUploadResult> IndexTextAsync(
        string text,
        string sourceName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private const int ChunkSize = 950;
    private const int ChunkOverlap = 160;

    private readonly IKnowledgeRepository _repository;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IEmbeddingService _embeddingService;

    public DocumentIndexingService(
        IKnowledgeRepository repository,
        IDocumentTextExtractor extractor,
        IEmbeddingService embeddingService)
    {
        _repository = repository;
        _extractor = extractor;
        _embeddingService = embeddingService;
    }

    public Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetDocumentsAsync(cancellationToken);
    }

    public async Task<DocumentUploadResult> IndexAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default)
    {
        if (fileStream.Length == 0)
        {
            throw new InvalidOperationException("The selected file is empty and cannot be indexed.");
        }

        Directory.CreateDirectory(uploadsRoot);
        await using var copy = new MemoryStream();
        await fileStream.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;

        var extractedText = await _extractor.ExtractAsync(copy, fileName, cancellationToken);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException("No readable text could be extracted from this document.");
        }

        var document = new IndexedDocument
        {
            FileName = Path.GetFileName(fileName),
            Subject = string.IsNullOrWhiteSpace(subject) ? "Demo course" : subject.Trim(),
            Chapter = string.IsNullOrWhiteSpace(chapter) ? "Uncategorized" : chapter.Trim(),
            ContentType = contentType,
            StoredPath = Path.Combine(uploadsRoot, $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}"),
            UploadedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = copy.Length,
            UploadedByUserId = uploader.UserId,
            UploadedByName = uploader.Name?.Trim() ?? string.Empty,
            UploadedByEmail = uploader.Email?.Trim() ?? string.Empty
        };

        if (Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(document.StoredPath, extractedText, Encoding.UTF8, cancellationToken);
        }
        else
        {
            copy.Position = 0;
            await using var savedFile = File.Create(document.StoredPath);
            await copy.CopyToAsync(savedFile, cancellationToken);
        }

        var chunkTexts = CreateChunks(extractedText);
        var chunks = new List<DocumentChunk>(chunkTexts.Count);
        for (var index = 0; index < chunkTexts.Count; index++)
        {
            var chunk = chunkTexts[index];
            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Subject = document.Subject,
                Chapter = document.Chapter,
                ChunkIndex = index + 1,
                Text = chunk,
                Embedding = await _embeddingService.EmbedAsync(chunk, cancellationToken)
            });
        }

        document.ChunkCount = chunks.Count;
        await _repository.AddDocumentAsync(document, chunks, cancellationToken);

        return new DocumentUploadResult(document.Id, chunks.Count, $"Indexed {chunks.Count} chunks from {document.FileName}.");
    }

    public async Task<DocumentUploadResult> IndexTextAsync(
        string text,
        string sourceName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("No readable text could be extracted from this source.");
        }

        Directory.CreateDirectory(uploadsRoot);
        var safeSourceName = MakeSafeSourceName(sourceName);
        var storedPath = Path.Combine(uploadsRoot, $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(storedPath, text, Encoding.UTF8, cancellationToken);

        var document = new IndexedDocument
        {
            FileName = safeSourceName,
            Subject = string.IsNullOrWhiteSpace(subject) ? "Demo course" : subject.Trim(),
            Chapter = string.IsNullOrWhiteSpace(chapter) ? "Uncategorized" : chapter.Trim(),
            ContentType = contentType,
            StoredPath = storedPath,
            UploadedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = Encoding.UTF8.GetByteCount(text),
            UploadedByUserId = uploader.UserId,
            UploadedByName = uploader.Name?.Trim() ?? string.Empty,
            UploadedByEmail = uploader.Email?.Trim() ?? string.Empty
        };

        var chunkTexts = CreateChunks(text);
        var chunks = new List<DocumentChunk>(chunkTexts.Count);
        for (var index = 0; index < chunkTexts.Count; index++)
        {
            var chunk = chunkTexts[index];
            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Subject = document.Subject,
                Chapter = document.Chapter,
                ChunkIndex = index + 1,
                Text = chunk,
                Embedding = await _embeddingService.EmbedAsync(chunk, cancellationToken)
            });
        }

        document.ChunkCount = chunks.Count;
        await _repository.AddDocumentAsync(document, chunks, cancellationToken);

        return new DocumentUploadResult(document.Id, chunks.Count, $"Indexed {chunks.Count} chunks from {document.FileName}.");
    }

    private static IReadOnlyList<string> CreateChunks(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Trim();
        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var remaining = normalized.Length - start;
            var length = Math.Min(ChunkSize, remaining);
            var end = start + length;

            if (end < normalized.Length)
            {
                var paragraphBreak = normalized.LastIndexOf("\n", end, length, StringComparison.Ordinal);
                if (paragraphBreak > start + ChunkSize / 2)
                {
                    end = paragraphBreak;
                }
            }

            var chunk = normalized[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(0, end - ChunkOverlap);
        }

        return chunks;
    }

    private static string MakeSafeSourceName(string sourceName)
    {
        var name = string.IsNullOrWhiteSpace(sourceName) ? "web-page" : sourceName.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidCharacter, '-');
        }

        return name.Length <= 120 ? name : name[..120];
    }
}
