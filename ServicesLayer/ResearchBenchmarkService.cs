using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record ResearchCatalog(
    IReadOnlyList<ResearchOption> EmbeddingModels,
    IReadOnlyList<ResearchOption> ChunkingStrategies);

public interface IResearchBenchmarkService
{
    Task<ResearchCatalog> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResearchExperimentSummary>> GetExperimentsAsync(CancellationToken cancellationToken = default);
    Task<ResearchExperimentDetail?> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken = default);
    Task<Guid> CreateExperimentAsync(CreateResearchExperimentRequest request, CancellationToken cancellationToken = default);
    Task RunExperimentAsync(Guid experimentId, CancellationToken cancellationToken = default);
}

public sealed class ResearchBenchmarkService : IResearchBenchmarkService
{
    private const int MaxGeminiRetryAttempts = 6;
    private const int GeminiEmbeddingBatchSize = 8;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IResearchRepository _researchRepository;
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly ILocalChatCompletionService _chatCompletionService;
    private readonly HttpClient _httpClient;
    private readonly GeminiApiOptions _geminiOptions;

    public ResearchBenchmarkService(
        IResearchRepository researchRepository,
        IKnowledgeRepository knowledgeRepository,
        ILocalChatCompletionService chatCompletionService,
        HttpClient httpClient,
        GeminiApiOptions geminiOptions)
    {
        _researchRepository = researchRepository;
        _knowledgeRepository = knowledgeRepository;
        _chatCompletionService = chatCompletionService;
        _httpClient = httpClient;
        _geminiOptions = geminiOptions;
    }

    public async Task<ResearchCatalog> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        return new ResearchCatalog(
            await _researchRepository.GetEmbeddingModelsAsync(cancellationToken),
            await _researchRepository.GetChunkingStrategiesAsync(cancellationToken));
    }

    public Task<IReadOnlyList<ResearchExperimentSummary>> GetExperimentsAsync(CancellationToken cancellationToken = default)
    {
        return _researchRepository.GetExperimentsAsync(cancellationToken);
    }

    public Task<ResearchExperimentDetail?> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        return _researchRepository.GetExperimentAsync(experimentId, cancellationToken);
    }

    public Task<Guid> CreateExperimentAsync(CreateResearchExperimentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Experiment name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new InvalidOperationException("Subject is required.");
        }

        return _researchRepository.CreateExperimentAsync(request, cancellationToken);
    }

    public async Task RunExperimentAsync(Guid experimentId, CancellationToken cancellationToken = default)
    {
        await _researchRepository.SetExperimentStatusAsync(experimentId, "Running", cancellationToken);
        var detail = await _researchRepository.GetExperimentAsync(experimentId, cancellationToken)
            ?? throw new InvalidOperationException("Experiment not found.");
        var runs = await _researchRepository.GetRunnableRunsAsync(experimentId, cancellationToken);
        var allChunks = await _knowledgeRepository.GetChunksAsync(cancellationToken);
        var subjectChunks = allChunks
            .Where(chunk => string.IsNullOrWhiteSpace(detail.Subject)
                || chunk.Subject.Contains(detail.Subject, StringComparison.OrdinalIgnoreCase)
                || detail.Subject.Contains(chunk.Subject, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (subjectChunks.Count == 0)
        {
            subjectChunks = allChunks.ToList();
        }

        var experimentFailed = false;
        foreach (var run in runs)
        {
            if (run.Status == "Done")
            {
                continue;
            }

            try
            {
                await _researchRepository.SetRunStatusAsync(run.Id, "Running", cancellationToken: cancellationToken);
                var results = run.RunKind.Equals("FineTuned", StringComparison.OrdinalIgnoreCase)
                    ? await RunFineTunedAsync(run, detail, cancellationToken)
                    : await RunRagAsync(run, detail, subjectChunks, cancellationToken);

                await _researchRepository.SaveBenchmarkResultsAsync(run.Id, results, cancellationToken);
                await _researchRepository.SetRunStatusAsync(run.Id, "Done", cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                experimentFailed = true;
                await _researchRepository.SetRunStatusAsync(run.Id, "Error", ex.Message, cancellationToken);
            }
        }

        await _researchRepository.SetExperimentStatusAsync(experimentId, experimentFailed ? "Failed" : "Completed", cancellationToken);
    }

    private async Task<IReadOnlyList<ResearchBenchmarkResult>> RunRagAsync(
        ResearchRunSummary run,
        ResearchExperimentDetail experiment,
        IReadOnlyList<DocumentChunk> originalChunks,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(run.EmbeddingModelName) || string.IsNullOrWhiteSpace(run.ChunkingStrategyName))
        {
            throw new InvalidOperationException("RAG run is missing embedding model or chunking strategy.");
        }

        var chunks = Rechunk(originalChunks, run);
        var chunkEmbeddings = await EmbedBatchAsync(
            run,
            chunks.Select(chunk => chunk.Text).ToList(),
            cancellationToken);
        for (var index = 0; index < chunks.Count; index++)
        {
            chunks[index].Embedding = chunkEmbeddings[index];
        }

        var queryEmbeddings = await EmbedBatchAsync(
            run,
            experiment.Questions.Select(question => question.Question).ToList(),
            cancellationToken);

        var results = new List<ResearchBenchmarkResult>();
        for (var questionIndex = 0; questionIndex < experiment.Questions.Count; questionIndex++)
        {
            var question = experiment.Questions[questionIndex];
            var stopwatch = Stopwatch.StartNew();
            var queryEmbedding = queryEmbeddings[questionIndex];
            var retrieved = chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = CosineSimilarity(queryEmbedding, chunk.Embedding)
                })
                .OrderByDescending(item => item.Score)
                .Take(5)
                .ToList();

            var answer = await _chatCompletionService.GenerateAnswerAsync(
                question.Question,
                experiment.Subject,
                Array.Empty<ChatMessage>(),
                retrieved.Select(item => item.Chunk).ToList(),
                "vi",
                cancellationToken) ?? "Không tạo được câu trả lời từ RAG.";
            stopwatch.Stop();

            var answerEmbedding = await EmbedAsync(run, answer, cancellationToken);
            var result = Score(
                question,
                answer,
                retrieved.Select(item => item.Chunk).ToList(),
                stopwatch.Elapsed.TotalMilliseconds,
                answerEmbedding,
                queryEmbedding);
            results.Add(result);
            await _researchRepository.SaveBenchmarkResultsAsync(run.Id, results, cancellationToken);
        }

        return results;
    }

    private async Task<IReadOnlyList<ResearchBenchmarkResult>> RunFineTunedAsync(
        ResearchRunSummary run,
        ResearchExperimentDetail experiment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(run.FineTunedEndpoint))
        {
            throw new InvalidOperationException("Fine-tuned run is missing endpoint.");
        }

        var results = new List<ResearchBenchmarkResult>();
        foreach (var question in experiment.Questions)
        {
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.PostAsJsonAsync(
                run.FineTunedEndpoint,
                new FineTunedRequest(experiment.Subject, question.Question),
                JsonOptions,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Fine-tuned endpoint returned {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<FineTunedResponse>(JsonOptions, cancellationToken);
            var answer = payload?.Answer ?? payload?.Text ?? string.Empty;
            stopwatch.Stop();
            results.Add(Score(question, answer, Array.Empty<DocumentChunk>(), stopwatch.Elapsed.TotalMilliseconds));
            await _researchRepository.SaveBenchmarkResultsAsync(run.Id, results, cancellationToken);
        }

        return results;
    }

    private async Task<Dictionary<int, double>> EmbedAsync(ResearchRunSummary run, string text, CancellationToken cancellationToken)
    {
        var embeddings = await EmbedBatchAsync(run, new[] { text }, cancellationToken);
        return embeddings[0];
    }

    private async Task<IReadOnlyList<Dictionary<int, double>>> EmbedBatchAsync(
        ResearchRunSummary run,
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<Dictionary<int, double>>();
        }

        if (run.EmbeddingProvider?.Equals("Gemini", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await EmbedWithGeminiBatchAsync(run, texts, cancellationToken);
        }

        throw new InvalidOperationException($"{run.EmbeddingModelName} uses unsupported provider {run.EmbeddingProvider}. Only Gemini embeddings are enabled.");
    }

    private async Task<Dictionary<int, double>> EmbedWithGeminiAsync(
        ResearchRunSummary run,
        string text,
        CancellationToken cancellationToken)
    {
        if (!_geminiOptions.Enabled || string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is required for RBL embeddings.");
        }

        var model = run.EmbeddingModelIdValue ?? _geminiOptions.EmbeddingModel;
        var outputDimensionality = ReadIntConfigValue(
            run.EmbeddingConfigJson,
            "outputDimensionality",
            _geminiOptions.EmbeddingOutputDimensionality);

        using var response = await SendGeminiWithRetryAsync(() =>
        {
            var retryRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:embedContent");
            retryRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _geminiOptions.ApiKey);
            retryRequest.Content = JsonContent.Create(
                new GeminiEmbeddingRequest(
                    $"models/{model}",
                    new GeminiEmbeddingContent([new GeminiEmbeddingPart(text)]),
                    Math.Max(1, outputDimensionality)),
                options: JsonOptions);
            return retryRequest;
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw BuildGeminiRuntimeException(run.EmbeddingModelName, "embedding", response);
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(JsonOptions, cancellationToken);
        if (payload?.Embedding?.Values is not { Count: > 0 } values)
        {
            throw new InvalidOperationException($"{run.EmbeddingModelName} returned an empty Gemini embedding.");
        }

        return GeminiEmbeddingService.NormalizeDenseEmbedding(values);
    }

    private async Task<IReadOnlyList<Dictionary<int, double>>> EmbedWithGeminiBatchAsync(
        ResearchRunSummary run,
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (!_geminiOptions.Enabled || string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is required for RBL embeddings.");
        }

        var model = run.EmbeddingModelIdValue ?? _geminiOptions.EmbeddingModel;
        var outputDimensionality = ReadIntConfigValue(
            run.EmbeddingConfigJson,
            "outputDimensionality",
            _geminiOptions.EmbeddingOutputDimensionality);
        var results = new List<Dictionary<int, double>>(texts.Count);

        foreach (var batch in texts.Chunk(GeminiEmbeddingBatchSize))
        {
            using var response = await SendGeminiWithRetryAsync(
                () => CreateGeminiBatchEmbeddingRequest(model, outputDimensionality, batch),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw BuildGeminiRuntimeException(run.EmbeddingModelName, "batch embedding", response);
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiBatchEmbeddingResponse>(JsonOptions, cancellationToken);
            if (payload?.Embeddings is not { Count: > 0 } embeddings || embeddings.Count != batch.Length)
            {
                throw new InvalidOperationException($"{run.EmbeddingModelName} returned an invalid Gemini batch embedding response.");
            }

            results.AddRange(embeddings.Select(embedding =>
            {
                if (embedding.Values is not { Count: > 0 } values)
                {
                    throw new InvalidOperationException($"{run.EmbeddingModelName} returned an empty Gemini embedding.");
                }

                return GeminiEmbeddingService.NormalizeDenseEmbedding(values);
            }));

            if (results.Count < texts.Count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
            }
        }

        return results;
    }

    private HttpRequestMessage CreateGeminiBatchEmbeddingRequest(
        string model,
        int outputDimensionality,
        IReadOnlyList<string> batch)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:batchEmbedContents");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _geminiOptions.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiBatchEmbeddingRequest(
                batch.Select(text => new GeminiEmbeddingRequest(
                        $"models/{model}",
                        new GeminiEmbeddingContent([new GeminiEmbeddingPart(text)]),
                        Math.Max(1, outputDimensionality)))
                    .ToList()),
            options: JsonOptions);
        return request;
    }

    private async Task<HttpResponseMessage> SendGeminiWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxGeminiRetryAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(createRequest(), cancellationToken);
                if (response.IsSuccessStatusCode || !IsRetryable(response) || attempt == MaxGeminiRetryAttempts)
                {
                    return response;
                }

                var delay = GetRetryDelay(response, attempt);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxGeminiRetryAttempts)
            {
                response?.Dispose();
                await Task.Delay(GetRetryDelay(null, attempt), cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxGeminiRetryAttempts)
            {
                response?.Dispose();
                await Task.Delay(GetRetryDelay(null, attempt), cancellationToken);
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }

        throw new InvalidOperationException("Gemini request could not be sent.");
    }

    private static bool IsRetryable(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        return statusCode is 429 or 500 or 502 or 503 or 504;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        var retryAfter = response?.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta <= TimeSpan.FromSeconds(60) ? delta : TimeSpan.FromSeconds(60);
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                return wait <= TimeSpan.FromSeconds(60) ? wait : TimeSpan.FromSeconds(60);
            }
        }

        var seconds = Math.Min(60, Math.Pow(2, attempt + 1));
        return TimeSpan.FromSeconds(seconds);
    }

    private static InvalidOperationException BuildGeminiRuntimeException(
        string? modelName,
        string operation,
        HttpResponseMessage response)
    {
        var name = string.IsNullOrWhiteSpace(modelName) ? "Gemini" : modelName;
        return (int)response.StatusCode == 429
            ? new InvalidOperationException($"{name} {operation} đang bị giới hạn quota/rate limit Gemini (HTTP 429). Hãy chờ 1-2 phút rồi chạy lại, hoặc giảm số câu hỏi/số chunking strategy trong lần chạy.")
            : new InvalidOperationException($"{name} Gemini {operation} runtime returned HTTP {(int)response.StatusCode}.");
    }

    private static int ReadIntConfigValue(string? configJson, string key, int fallback)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (!document.RootElement.TryGetProperty(key, out var value))
            {
                return fallback;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var numberValue) => numberValue,
                JsonValueKind.String when int.TryParse(value.GetString(), out var stringValue) => stringValue,
                _ => fallback
            };
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static List<DocumentChunk> Rechunk(IReadOnlyList<DocumentChunk> originalChunks, ResearchRunSummary run)
    {
        var documents = originalChunks
            .GroupBy(chunk => new { chunk.DocumentId, chunk.FileName, chunk.Subject, chunk.Chapter })
            .Select(group => new
            {
                group.Key.DocumentId,
                group.Key.FileName,
                group.Key.Subject,
                group.Key.Chapter,
                Text = string.Join("\n\n", group.OrderBy(chunk => chunk.ChunkIndex).Select(chunk => chunk.Text))
            });

        var output = new List<DocumentChunk>();
        foreach (var document in documents)
        {
            var texts = SplitText(document.Text, run.ChunkingMethod ?? "Paragraph", Math.Max(200, run.ChunkSize), Math.Max(0, run.ChunkOverlap));
            for (var index = 0; index < texts.Count; index++)
            {
                output.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.DocumentId,
                    FileName = document.FileName,
                    Subject = document.Subject,
                    Chapter = document.Chapter,
                    ChunkIndex = index + 1,
                    Text = texts[index]
                });
            }
        }

        return output;
    }

    private static IReadOnlyList<string> SplitText(string text, string method, int chunkSize, int overlap)
    {
        var normalized = text.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        if (method.Equals("Paragraph", StringComparison.OrdinalIgnoreCase)
            || method.Equals("SemanticLite", StringComparison.OrdinalIgnoreCase))
        {
            var chunks = new List<string>();
            var current = new StringBuilder();
            foreach (var paragraph in Regex.Split(normalized, @"\n\s*\n").Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                if (current.Length > 0 && current.Length + paragraph.Length > chunkSize)
                {
                    chunks.Add(current.ToString().Trim());
                    current.Clear();
                }

                current.AppendLine(paragraph.Trim());
                current.AppendLine();
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
            }

            return chunks.Count == 0 ? SplitFixed(normalized, chunkSize, overlap) : chunks;
        }

        return SplitFixed(normalized, chunkSize, method.Equals("SlidingWindow", StringComparison.OrdinalIgnoreCase) ? overlap : 0);
    }

    private static IReadOnlyList<string> SplitFixed(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            var chunk = text.Substring(start, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (start + length >= text.Length)
            {
                break;
            }

            start = Math.Max(start + 1, start + length - overlap);
        }

        return chunks;
    }

    private static ResearchBenchmarkResult Score(
        ResearchTestQuestion question,
        string answer,
        IReadOnlyList<DocumentChunk> retrievedChunks,
        double latencyMs)
    {
        return Score(question, answer, retrievedChunks, latencyMs, null, null);
    }

    private static ResearchBenchmarkResult Score(
        ResearchTestQuestion question,
        string answer,
        IReadOnlyList<DocumentChunk> retrievedChunks,
        double latencyMs,
        IReadOnlyDictionary<int, double>? answerEmbedding,
        IReadOnlyDictionary<int, double>? questionEmbedding)
    {
        var groundTruthTerms = ExtractTerms(question.GroundTruth);
        var answerTerms = ExtractTerms(answer);
        var contextTerms = ExtractTerms(string.Join(" ", retrievedChunks.Select(item => item.Text)));

        var answerGroundTruthOverlap = BalancedOverlap(answerTerms, groundTruthTerms);
        var answerQuestionOverlap = BalancedOverlap(answerTerms, ExtractTerms(question.Question));
        var answerSemanticSimilarity = answerEmbedding is not null && questionEmbedding is not null
            ? NormalizeCosine(CosineSimilarity(answerEmbedding, questionEmbedding))
            : answerGroundTruthOverlap;
        var answerRelevancy = Clamp01((answerGroundTruthOverlap * 0.55) + (answerSemanticSimilarity * 0.30) + (answerQuestionOverlap * 0.15));

        var contextRecall = groundTruthTerms.Count == 0 ? 0 : BalancedOverlap(contextTerms, groundTruthTerms);
        var contextPrecision = contextTerms.Count == 0
            ? 0
            : Math.Max(BalancedOverlap(groundTruthTerms, contextTerms), BalancedOverlap(answerTerms, contextTerms));
        var faithfulness = retrievedChunks.Count == 0
            ? answerRelevancy
            : Clamp01((BalancedOverlap(answerTerms, contextTerms) * 0.75) + (contextRecall * 0.25));
        var ragas = (faithfulness * 0.35) + (answerRelevancy * 0.25) + (contextPrecision * 0.20) + (contextRecall * 0.20);

        return new ResearchBenchmarkResult
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Question = question.Question,
            GroundTruth = question.GroundTruth,
            GeneratedAnswer = string.IsNullOrWhiteSpace(answer) ? "(empty answer)" : answer,
            Faithfulness = Math.Round(faithfulness, 4),
            AnswerRelevancy = Math.Round(answerRelevancy, 4),
            ContextPrecision = Math.Round(contextPrecision, 4),
            ContextRecall = Math.Round(contextRecall, 4),
            RagasScore = Math.Round(ragas, 4),
            LatencyMs = Math.Round(latencyMs, 2),
            RetrievedChunksJson = JsonSerializer.Serialize(
                retrievedChunks.Select(chunk => new
                {
                    chunk.DocumentId,
                    chunk.FileName,
                    chunk.Chapter,
                    chunk.ChunkIndex,
                    Excerpt = chunk.Text.Length <= 240 ? chunk.Text : chunk.Text[..240]
                }),
                JsonOptions),
            EvaluatedAt = DateTimeOffset.UtcNow
        };
    }

    private static HashSet<string> ExtractTerms(string text)
    {
        return Regex.Matches(RemoveDiacritics(text).ToLowerInvariant(), @"[\p{L}\p{N}]{2,}")
            .Select(match => match.Value)
            .Select(NormalizeTerm)
            .Where(term => !StopWords.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static double Overlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        return left.Count(item => right.Contains(item)) / (double)left.Count;
    }

    private static double BalancedOverlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var leftHit = left.Count(item => right.Any(other => TermsMatch(item, other))) / (double)left.Count;
        var rightHit = right.Count(item => left.Any(other => TermsMatch(item, other))) / (double)right.Count;
        return (leftHit + rightHit) / 2d;
    }

    private static bool TermsMatch(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return left.Length >= 4
               && right.Length >= 4
               && (left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
                   || right.StartsWith(left, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTerm(string term)
    {
        var value = term.Trim();
        foreach (var suffix in new[] { "ing", "tion", "ment", "ness", "ed", "s" })
        {
            if (value.Length > suffix.Length + 3 && value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return value[..^suffix.Length];
            }
        }

        return value;
    }

    private static double NormalizeCosine(double cosine)
    {
        return Clamp01((cosine + 1d) / 2d);
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, value));
    }

    private static double CosineSimilarity(IReadOnlyDictionary<int, double> left, IReadOnlyDictionary<int, double> right)
    {
        var smaller = left.Count < right.Count ? left : right;
        var larger = ReferenceEquals(smaller, left) ? right : left;
        return smaller.Sum(item => larger.TryGetValue(item.Key, out var value) ? item.Value * value : 0);
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character == '\u0111' || character == '\u0110' ? 'd' : character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "for", "you", "your", "are", "was", "were",
        "mot", "cac", "nhung", "duoc", "trong", "ngoai", "theo", "cua", "cho", "voi", "khong", "la", "va"
    };

    private sealed record GeminiEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("content")] GeminiEmbeddingContent Content,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality);

    private sealed record GeminiEmbeddingContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiEmbeddingPart> Parts);

    private sealed record GeminiEmbeddingPart(
        [property: JsonPropertyName("text")] string Text);

    private sealed record GeminiEmbeddingResponse(
        [property: JsonPropertyName("embedding")] GeminiEmbedding? Embedding);

    private sealed record GeminiBatchEmbeddingRequest(
        [property: JsonPropertyName("requests")] IReadOnlyList<GeminiEmbeddingRequest> Requests);

    private sealed record GeminiBatchEmbeddingResponse(
        [property: JsonPropertyName("embeddings")] List<GeminiEmbedding>? Embeddings);

    private sealed record GeminiEmbedding(
        [property: JsonPropertyName("values")] List<double>? Values);

    private sealed record FineTunedRequest(
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("question")] string Question);

    private sealed record FineTunedResponse(
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("text")] string? Text);
}
