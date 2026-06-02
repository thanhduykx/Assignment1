using System.Text;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record DocumentUploadResult(Guid DocumentId, int ChunkCount, string Message);

public sealed record DocumentUploaderInfo(Guid? UserId, string? Name, string? Email);

public interface IDocumentIndexingService
{
    Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<DocumentUploadResult> QueueFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default);
    Task<DocumentUploadResult> QueueTextAsync(
        string text,
        string sourceName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default);
    Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IKnowledgeRepository _repository;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITextChunker _chunker;

    public DocumentIndexingService(
        IKnowledgeRepository repository,
        IDocumentTextExtractor extractor,
        IEmbeddingService embeddingService,
        ITextChunker chunker)
    {
        _repository = repository;
        _extractor = extractor;
        _embeddingService = embeddingService;
        _chunker = chunker;
    }

    public Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetDocumentsAsync(cancellationToken);
    }

    public async Task<DocumentUploadResult> QueueFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string subject,
        string chapter,
        string uploadsRoot,
        DocumentUploaderInfo uploader,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(uploadsRoot);

        await using var copy = new MemoryStream();
        await fileStream.CopyToAsync(copy, cancellationToken);
        if (copy.Length == 0)
        {
            throw new InvalidOperationException("The selected file is empty and cannot be indexed.");
        }

        var safeFileName = NormalizeFileName(fileName);
        var storedPath = Path.Combine(uploadsRoot, $"{Guid.NewGuid():N}{Path.GetExtension(safeFileName)}");
        copy.Position = 0;
        await using (var savedFile = File.Create(storedPath))
        {
            await copy.CopyToAsync(savedFile, cancellationToken);
        }

        var document = CreateProcessingDocument(
            safeFileName,
            contentType,
            subject,
            chapter,
            storedPath,
            copy.Length,
            uploader);

        await _repository.AddDocumentAsync(document, Array.Empty<DocumentChunk>(), cancellationToken);
        return new DocumentUploadResult(document.Id, 0, $"Queued {document.FileName} for indexing.");
    }

    public async Task<DocumentUploadResult> QueueTextAsync(
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

        var document = CreateProcessingDocument(
            safeSourceName,
            contentType,
            subject,
            chapter,
            storedPath,
            Encoding.UTF8.GetByteCount(text),
            uploader);

        await _repository.AddDocumentAsync(document, Array.Empty<DocumentChunk>(), cancellationToken);
        return new DocumentUploadResult(document.Id, 0, $"Queued {document.FileName} for indexing.");
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        if (document.Status == DocumentIndexStatus.Indexed && document.ChunkCount > 0)
        {
            return;
        }

        await _repository.MarkDocumentIndexProcessingAsync(document.Id, cancellationToken);

        var storedPath = Path.GetFullPath(document.StoredPath);
        if (!File.Exists(storedPath))
        {
            throw new InvalidOperationException("Stored file was not found for indexing.");
        }

        string extractedText;
        await using (var stream = File.OpenRead(storedPath))
        {
            extractedText = await _extractor.ExtractAsync(stream, document.FileName, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException("No readable text could be extracted from this document.");
        }

        var chunkTexts = _chunker.CreateChunks(extractedText);
        if (chunkTexts.Count == 0)
        {
            throw new InvalidOperationException("No indexable chunks could be created from this document.");
        }

        var chunks = new List<DocumentChunk>(chunkTexts.Count);
        foreach (var chunk in chunkTexts)
        {
            chunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Subject = document.Subject,
                Chapter = document.Chapter,
                ChunkIndex = chunk.ChunkIndex,
                Text = chunk.Text,
                SectionTitle = chunk.SectionTitle,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd,
                Embedding = await _embeddingService.EmbedAsync(chunk.Text, cancellationToken)
            });
        }

        await _repository.CompleteDocumentIndexAsync(
            document.Id,
            chunks,
            _embeddingService.ModelName,
            _embeddingService.Dimensions,
            _chunker.StrategyName,
            cancellationToken);
    }

    private IndexedDocument CreateProcessingDocument(
        string fileName,
        string contentType,
        string subject,
        string chapter,
        string storedPath,
        long fileSizeBytes,
        DocumentUploaderInfo uploader)
    {
        return new IndexedDocument
        {
            FileName = NormalizeFileName(fileName),
            Subject = NormalizeRequiredText(subject, "Subject is required."),
            Chapter = NormalizeRequiredText(chapter, "Chapter is required."),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            StoredPath = storedPath,
            UploadedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = fileSizeBytes,
            UploadedByUserId = uploader.UserId,
            UploadedByName = uploader.Name?.Trim() ?? string.Empty,
            UploadedByEmail = uploader.Email?.Trim() ?? string.Empty,
            Status = DocumentIndexStatus.Processing,
            ChunkCount = 0,
            IndexedAt = null,
            IndexError = string.Empty,
            EmbeddingModel = _embeddingService.ModelName,
            EmbeddingDimensions = _embeddingService.Dimensions,
            ChunkingStrategy = _chunker.StrategyName
        };
    }

    private static string NormalizeFileName(string fileName)
    {
        var normalized = Path.GetFileName((fileName ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("File name is required.");
        }

        return normalized;
    }

    private static string NormalizeRequiredText(string value, string errorMessage)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static string MakeSafeSourceName(string sourceName)
    {
        var name = string.IsNullOrWhiteSpace(sourceName) ? "web-page.txt" : sourceName.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidCharacter, '-');
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
        {
            name += ".txt";
        }

        return name.Length <= 120 ? name : name[..120];
    }
}
