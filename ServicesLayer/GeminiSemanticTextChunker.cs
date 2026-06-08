using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ServicesLayer;

public sealed record GeminiSemanticChunkingOptions(
    string ApiKey,
    string Model,
    bool Enabled,
    int MaxPromptCharacters,
    int MaxParagraphs);

public sealed class GeminiSemanticTextChunker : ITextChunker
{
    private const int TargetSize = 950;
    private const int MaxSize = 1200;
    private const int Overlap = 0;
    private const int ParagraphPreviewLength = 220;
    private const int LongBlockSplitSize = 900;

    private static readonly Regex SpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex CodeFenceRegex = new(@"^```(?:json)?\s*|\s*```$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiSemanticChunkingOptions _options;
    private readonly ITextChunker _fallback;

    public GeminiSemanticTextChunker(
        HttpClient httpClient,
        GeminiSemanticChunkingOptions options,
        ITextChunker fallback)
    {
        _httpClient = httpClient;
        _options = options;
        _fallback = fallback;
    }

    public string StrategyName => $"gemini-assisted-{TargetSize}-{Overlap}";

    public IReadOnlyList<TextChunk> CreateChunks(string text)
    {
        return _fallback.CreateChunks(text);
    }

    public async Task<TextChunkingResult> CreateChunkingResultAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return await _fallback.CreateChunkingResultAsync(text, cancellationToken);
        }

        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new TextChunkingResult(Array.Empty<TextChunk>(), StrategyName);
        }

        var blocks = CreateBlocks(normalized);
        if (blocks.Count == 0)
        {
            return await _fallback.CreateChunkingResultAsync(text, cancellationToken);
        }

        if (blocks.Count > Math.Max(1, _options.MaxParagraphs))
        {
            return await _fallback.CreateChunkingResultAsync(text, cancellationToken);
        }

        var prompt = BuildPrompt(blocks);
        if (prompt.Length > Math.Max(1000, _options.MaxPromptCharacters))
        {
            return await _fallback.CreateChunkingResultAsync(text, cancellationToken);
        }

        try
        {
            var sections = await RequestSectionsAsync(prompt, blocks.Count, cancellationToken);
            var chunks = BuildChunksFromSections(blocks, sections);
            return chunks.Count == 0
                ? await _fallback.CreateChunkingResultAsync(text, cancellationToken)
                : new TextChunkingResult(chunks, StrategyName);
        }
        catch (Exception ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await _fallback.CreateChunkingResultAsync(text, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<SemanticSection>> RequestSectionsAsync(
        string prompt,
        int blockCount,
        CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "gemini-3.5-flash"
            : _options.Model.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiGenerateContentRequest(
                [new GeminiContent("user", [new GeminiPart(prompt)])],
                new GeminiGenerationConfig(0, 4096, "application/json")),
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini semantic chunking returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(JsonOptions, cancellationToken);
        var json = payload?.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part.Text))?
            .Text;

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Gemini semantic chunking returned an empty response.");
        }

        var cleanedJson = CleanJson(json);
        var semantic = JsonSerializer.Deserialize<GeminiSemanticChunkingResponse>(cleanedJson, JsonOptions);
        var sections = ValidateSections(semantic?.Sections, blockCount);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException("Gemini semantic chunking did not return valid sections.");
        }

        return sections;
    }

    private static string BuildPrompt(IReadOnlyList<TextBlock> blocks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You group document paragraphs into semantic sections for RAG indexing.");
        builder.AppendLine("Return only JSON with this shape: {\"sections\":[{\"title\":\"short section title\",\"paragraphIds\":[1,2]}]}.");
        builder.AppendLine("Rules:");
        builder.AppendLine("- Use every paragraph id exactly once.");
        builder.AppendLine("- paragraphIds must be contiguous and ascending inside each section.");
        builder.AppendLine("- Keep original order. Do not summarize or rewrite paragraph text.");
        builder.AppendLine("- Prefer headings/course topics as section titles. Keep titles under 80 chars.");
        builder.AppendLine();
        builder.AppendLine("Paragraph outline:");

        foreach (var block in blocks)
        {
            var preview = block.Text.Length <= ParagraphPreviewLength
                ? block.Text
                : $"{block.Text[..ParagraphPreviewLength]}...";
            builder.Append(block.Id);
            builder.Append(". len=");
            builder.Append(block.Text.Length);
            if (IsHeading(block.Text))
            {
                builder.Append(" heading=true");
            }

            builder.Append(" text=\"");
            builder.Append(preview.Replace("\"", "'"));
            builder.AppendLine("\"");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<TextChunk> BuildChunksFromSections(
        IReadOnlyList<TextBlock> blocks,
        IReadOnlyList<SemanticSection> sections)
    {
        var chunks = new List<TextChunk>();
        var byId = blocks.ToDictionary(block => block.Id);

        foreach (var section in sections)
        {
            var sectionBlocks = section.ParagraphIds
                .Select(id => byId[id])
                .OrderBy(block => block.Id)
                .ToList();

            var builder = new StringBuilder();
            var chunkStart = 0;
            var chunkEnd = 0;
            var title = NormalizeTitle(section.Title, sectionBlocks.FirstOrDefault()?.Text ?? string.Empty);

            foreach (var block in sectionBlocks)
            {
                if (builder.Length == 0)
                {
                    chunkStart = block.Start;
                }

                var separatorLength = builder.Length == 0 ? 0 : Environment.NewLine.Length * 2;
                if (builder.Length > 0 && builder.Length + separatorLength + block.Text.Length > MaxSize)
                {
                    AddChunk(chunks, builder.ToString(), title, chunkStart, chunkEnd);
                    builder.Clear();
                    chunkStart = block.Start;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.Append(block.Text);
                chunkEnd = block.End;
            }

            AddChunk(chunks, builder.ToString(), title, chunkStart, chunkEnd);
        }

        return chunks;
    }

    private static IReadOnlyList<SemanticSection> ValidateSections(
        IReadOnlyList<GeminiSemanticSection>? sections,
        int blockCount)
    {
        if (sections is null || sections.Count == 0)
        {
            return Array.Empty<SemanticSection>();
        }

        var result = new List<SemanticSection>();
        var expectedId = 1;
        foreach (var section in sections)
        {
            var ids = section.ParagraphIds?
                .Where(id => id > 0 && id <= blockCount)
                .Distinct()
                .ToList();
            if (ids is null || ids.Count == 0)
            {
                return Array.Empty<SemanticSection>();
            }

            if (!ids.SequenceEqual(ids.OrderBy(id => id)))
            {
                return Array.Empty<SemanticSection>();
            }

            foreach (var id in ids)
            {
                if (id != expectedId)
                {
                    return Array.Empty<SemanticSection>();
                }

                expectedId++;
            }

            result.Add(new SemanticSection(section.Title ?? string.Empty, ids));
        }

        return expectedId == blockCount + 1 ? result : Array.Empty<SemanticSection>();
    }

    private static IReadOnlyList<TextBlock> CreateBlocks(string normalized)
    {
        var blocks = new List<TextBlock>();
        var searchStart = 0;

        foreach (var line in normalized.Split('\n'))
        {
            var blockText = line.Trim();
            if (string.IsNullOrWhiteSpace(blockText))
            {
                searchStart += line.Length + 1;
                continue;
            }

            var start = normalized.IndexOf(blockText, searchStart, StringComparison.Ordinal);
            if (start < 0)
            {
                start = Math.Min(searchStart, normalized.Length);
            }

            var end = Math.Min(normalized.Length, start + blockText.Length);
            searchStart = Math.Min(normalized.Length, end + 1);
            blocks.AddRange(SplitLongBlock(blockText, start));
        }

        return blocks
            .Select((block, index) => block with { Id = index + 1 })
            .ToList();
    }

    private static IEnumerable<TextBlock> SplitLongBlock(string text, int blockStart)
    {
        var offset = 0;
        while (offset < text.Length)
        {
            var remaining = text.Length - offset;
            var length = Math.Min(LongBlockSplitSize, remaining);
            var end = offset + length;

            if (end < text.Length)
            {
                var boundary = text.LastIndexOf(' ', end - 1, length);
                if (boundary > offset + LongBlockSplitSize / 2)
                {
                    end = boundary;
                }
            }

            var slice = text[offset..end].Trim();
            if (!string.IsNullOrWhiteSpace(slice))
            {
                yield return new TextBlock(0, slice, blockStart + offset, blockStart + end);
            }

            offset = end;
            while (offset < text.Length && char.IsWhiteSpace(text[offset]))
            {
                offset++;
            }
        }
    }

    private static void AddChunk(List<TextChunk> chunks, string text, string sectionTitle, int start, int end)
    {
        var chunkText = text.Trim();
        if (string.IsNullOrWhiteSpace(chunkText))
        {
            return;
        }

        chunks.Add(new TextChunk(
            chunks.Count + 1,
            chunkText,
            sectionTitle,
            Math.Max(0, start),
            Math.Max(start, end)));
    }

    private static bool IsHeading(string line)
    {
        return line.Length <= 120
               && (line.EndsWith(":", StringComparison.Ordinal)
                   || Regex.IsMatch(line, @"^\d+(\.\d+)*[\).:-]?\s+\S+")
                   || Regex.IsMatch(line, @"^(chapter|section|unit|lesson|week|module|part)\b", RegexOptions.IgnoreCase));
    }

    private static string NormalizeTitle(string title, string fallbackText)
    {
        var normalized = SpaceRegex.Replace((title ?? string.Empty).Trim(), " ");
        if (string.IsNullOrWhiteSpace(normalized) && IsHeading(fallbackText))
        {
            normalized = fallbackText.Trim();
        }

        if (normalized.Length > 120)
        {
            normalized = normalized[..120].Trim();
        }

        return normalized;
    }

    private static string Normalize(string text)
    {
        var lines = (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => SpaceRegex.Replace(line.Trim(), " "));

        return string.Join('\n', lines).Trim();
    }

    private static string CleanJson(string json)
    {
        return CodeFenceRegex.Replace(json.Trim(), string.Empty).Trim();
    }

    private static bool ShouldFallback(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is InvalidOperationException
            or HttpRequestException
            or TaskCanceledException
            or JsonException;
    }

    private sealed record TextBlock(int Id, string Text, int Start, int End);
    private sealed record SemanticSection(string Title, IReadOnlyList<int> ParagraphIds);

    private sealed record GeminiSemanticChunkingResponse(
        [property: JsonPropertyName("sections")] IReadOnlyList<GeminiSemanticSection>? Sections);

    private sealed record GeminiSemanticSection(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("paragraphIds")] IReadOnlyList<int>? ParagraphIds);

    private sealed record GeminiGenerateContentRequest(
        [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string Text);

    private sealed record GeminiGenerationConfig(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
        [property: JsonPropertyName("responseMimeType")] string ResponseMimeType);

    private sealed record GeminiGenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content);
}
