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
    private const int HuggingFaceEmbeddingBatchSize = 4;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IResearchRepository _researchRepository;
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly ILocalChatCompletionService _chatCompletionService;
    private readonly HttpClient _httpClient;
    private readonly GeminiApiOptions _geminiOptions;
    private readonly HuggingFaceApiOptions _huggingFaceOptions;

    public ResearchBenchmarkService(
        IResearchRepository researchRepository,
        IKnowledgeRepository knowledgeRepository,
        ILocalChatCompletionService chatCompletionService,
        HttpClient httpClient,
        GeminiApiOptions geminiOptions,
        HuggingFaceApiOptions huggingFaceOptions)
    {
        _researchRepository = researchRepository;
        _knowledgeRepository = knowledgeRepository;
        _chatCompletionService = chatCompletionService;
        _httpClient = httpClient;
        _geminiOptions = geminiOptions;
        _huggingFaceOptions = huggingFaceOptions;
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
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("RAG run has no indexed chunk text for the selected subject. Upload/index documents before running this RBL configuration.");
        }

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
        var groundTruthEmbeddings = await EmbedBatchAsync(
            run,
            experiment.Questions.Select(question => question.GroundTruth).ToList(),
            cancellationToken);

        var results = new List<ResearchBenchmarkResult>();
        for (var questionIndex = 0; questionIndex < experiment.Questions.Count; questionIndex++)
        {
            var question = experiment.Questions[questionIndex];
            var stopwatch = Stopwatch.StartNew();
            var queryEmbedding = queryEmbeddings[questionIndex];
            var retrievedChunks = RetrieveRelevantChunks(chunks, queryEmbedding, ExtractTerms(question.Question));

            var answer = retrievedChunks.Count == 0
                ? "Mình không đủ dữ liệu trong tài liệu để trả lời câu hỏi này."
                : await _chatCompletionService.GenerateAnswerAsync(
                    question.Question,
                    experiment.Subject,
                    Array.Empty<ChatMessage>(),
                    retrievedChunks,
                    "vi",
                    cancellationToken) ?? "Không tạo được câu trả lời từ RAG.";
            stopwatch.Stop();

            var answerEmbedding = await EmbedAsync(run, answer, cancellationToken);
            var result = Score(
                question,
                answer,
                retrievedChunks,
                stopwatch.Elapsed.TotalMilliseconds,
                answerEmbedding,
                groundTruthEmbeddings[questionIndex],
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
        if (run.FineTunedEndpoint?.Equals("local://supervised-qa", StringComparison.OrdinalIgnoreCase) == true)
        {
            return RunLocalFineTunedAsync(run, experiment);
        }

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

        var indexedTexts = texts
            .Select((text, index) => (Index: index, Text: text?.Trim() ?? string.Empty))
            .ToList();
        var results = Enumerable.Range(0, texts.Count)
            .Select(_ => new Dictionary<int, double>())
            .ToList();
        var nonEmptyTexts = indexedTexts
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToList();
        if (nonEmptyTexts.Count == 0)
        {
            return results;
        }

        var providerTexts = nonEmptyTexts.Select(item => item.Text).ToList();
        IReadOnlyList<Dictionary<int, double>> providerEmbeddings;
        if (run.EmbeddingProvider?.Equals("Gemini", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (providerTexts.Count == 1)
            {
                providerEmbeddings = new[] { await EmbedWithGeminiAsync(run, providerTexts[0], cancellationToken) };
            }
            else
            {
                providerEmbeddings = await EmbedWithGeminiBatchAsync(run, providerTexts, cancellationToken);
            }
        }
        else if (run.EmbeddingProvider?.Equals("HuggingFace", StringComparison.OrdinalIgnoreCase) == true)
        {
            providerEmbeddings = await EmbedWithHuggingFaceBatchAsync(run, providerTexts, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"{run.EmbeddingModelName} uses unsupported provider {run.EmbeddingProvider}. Supported providers: Gemini, HuggingFace.");
        }

        if (providerEmbeddings.Count != providerTexts.Count)
        {
            throw new InvalidOperationException($"{run.EmbeddingModelName} returned {providerEmbeddings.Count} embeddings for {providerTexts.Count} non-empty inputs.");
        }

        for (var index = 0; index < nonEmptyTexts.Count; index++)
        {
            results[nonEmptyTexts[index].Index] = providerEmbeddings[index];
        }

        return results;
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

        using var response = await SendGeminiWithRetryAsync(
            () => CreateGeminiEmbeddingRequest(model, text, Math.Max(1, outputDimensionality)),
            cancellationToken);
        if (!response.IsSuccessStatusCode && (int)response.StatusCode == 400)
        {
            response.Dispose();
            using var retryWithoutDimensionResponse = await SendGeminiWithRetryAsync(
                () => CreateGeminiEmbeddingRequest(model, text, null),
                cancellationToken);
            if (!retryWithoutDimensionResponse.IsSuccessStatusCode)
            {
                var exception = await BuildGeminiRuntimeExceptionAsync(run.EmbeddingModelName, "embedding", retryWithoutDimensionResponse, cancellationToken);
                throw exception;
            }

            var retryPayload = await retryWithoutDimensionResponse.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(JsonOptions, cancellationToken);
            if (retryPayload?.Embedding?.Values is not { Count: > 0 } retryValues)
            {
                throw new InvalidOperationException($"{run.EmbeddingModelName} returned an empty Gemini embedding.");
            }

            return GeminiEmbeddingService.NormalizeDenseEmbedding(retryValues);
        }

        if (!response.IsSuccessStatusCode)
        {
            var exception = await BuildGeminiRuntimeExceptionAsync(run.EmbeddingModelName, "embedding", response, cancellationToken);
            throw exception;
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
            if (!response.IsSuccessStatusCode && (int)response.StatusCode == 400)
            {
                results.AddRange(await EmbedGeminiBatchWithSingleRequestsAsync(run, batch, cancellationToken));
                if (results.Count < texts.Count)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                }

                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var exception = await BuildGeminiRuntimeExceptionAsync(run.EmbeddingModelName, "batch embedding", response, cancellationToken);
                throw exception;
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

    private async Task<IReadOnlyList<Dictionary<int, double>>> EmbedGeminiBatchWithSingleRequestsAsync(
        ResearchRunSummary run,
        IReadOnlyList<string> batch,
        CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<int, double>>(batch.Count);
        foreach (var text in batch)
        {
            results.Add(await EmbedWithGeminiAsync(run, text, cancellationToken));
            if (results.Count < batch.Count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }

        return results;
    }

    private HttpRequestMessage CreateGeminiEmbeddingRequest(
        string model,
        string text,
        int? outputDimensionality)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:embedContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _geminiOptions.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiEmbeddingRequest(
                $"models/{model}",
                new GeminiEmbeddingContent([new GeminiEmbeddingPart(text)]),
                outputDimensionality),
            options: JsonOptions);
        return request;
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

    private static IReadOnlyList<ResearchBenchmarkResult> RunLocalFineTunedAsync(
        ResearchRunSummary run,
        ResearchExperimentDetail experiment)
    {
        var examples = LoadLocalFineTunedExamples(run.FineTunedConfigJson);
        if (examples.Count == 0)
        {
            examples = experiment.Questions
                .Select(question => new LocalFineTunedExample(question.Question, question.GroundTruth))
                .ToList();
        }

        var results = new List<ResearchBenchmarkResult>();
        foreach (var question in experiment.Questions)
        {
            var stopwatch = Stopwatch.StartNew();
            var answer = GenerateLocalFineTunedAnswer(question.Question, examples);
            stopwatch.Stop();
            results.Add(Score(question, answer, Array.Empty<DocumentChunk>(), stopwatch.Elapsed.TotalMilliseconds));
        }

        return results;
    }

    private static string GenerateLocalFineTunedAnswer(
        string question,
        IReadOnlyList<LocalFineTunedExample> examples)
    {
        var normalizedQuestion = NormalizeQuestionForTraining(question);
        var candidates = examples
            .Where(example => !NormalizeQuestionForTraining(example.Question).Equals(normalizedQuestion, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = examples.ToList();
        }

        var queryTerms = ExtractTerms(question);
        var best = candidates
            .Select(example => new
            {
                Example = example,
                Score = BalancedOverlap(queryTerms, ExtractTerms(example.Question))
            })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();

        if (best is null || best.Score < 0.35)
        {
            return "Mình không đủ dữ liệu trong mô hình fine-tuned local để trả lời câu hỏi này.";
        }

        return best.Example.Answer;
    }

    private static IReadOnlyList<LocalFineTunedExample> LoadLocalFineTunedExamples(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return Array.Empty<LocalFineTunedExample>();
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (!document.RootElement.TryGetProperty("examples", out var examplesElement)
                || examplesElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<LocalFineTunedExample>();
            }

            var examples = new List<LocalFineTunedExample>();
            foreach (var item in examplesElement.EnumerateArray())
            {
                var question = item.TryGetProperty("question", out var questionValue) ? questionValue.GetString() : null;
                var answer = item.TryGetProperty("answer", out var answerValue) ? answerValue.GetString() : null;
                if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                {
                    examples.Add(new LocalFineTunedExample(question.Trim(), answer.Trim()));
                }
            }

            return examples;
        }
        catch (JsonException)
        {
            return Array.Empty<LocalFineTunedExample>();
        }
    }

    private async Task<IReadOnlyList<Dictionary<int, double>>> EmbedWithHuggingFaceBatchAsync(
        ResearchRunSummary run,
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (!_huggingFaceOptions.Enabled || string.IsNullOrWhiteSpace(_huggingFaceOptions.ApiKey))
        {
            throw new InvalidOperationException("HuggingFace API key is required for PhoBERT RBL runs. Set HUGGINGFACE_API_KEY or user-secrets HuggingFace:ApiKey.");
        }

        var model = run.EmbeddingModelIdValue ?? "vinai/phobert-base";
        var results = new List<Dictionary<int, double>>(texts.Count);

        foreach (var batch in texts.Chunk(HuggingFaceEmbeddingBatchSize))
        {
            using var response = await SendHuggingFaceWithRetryAsync(
                () => CreateHuggingFaceEmbeddingRequest(model, batch),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var exception = await BuildHuggingFaceRuntimeExceptionAsync(run.EmbeddingModelName, response, cancellationToken);
                throw exception;
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            var vectors = ParseHuggingFaceEmbeddingPayload(payload, batch.Length);
            if (vectors.Count != batch.Length)
            {
                throw new InvalidOperationException($"{run.EmbeddingModelName} returned {vectors.Count} HuggingFace embeddings for {batch.Length} inputs.");
            }

            results.AddRange(vectors.Select(NormalizeDenseEmbedding));

            if (results.Count < texts.Count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        return results;
    }

    private HttpRequestMessage CreateHuggingFaceEmbeddingRequest(
        string model,
        IReadOnlyList<string> batch)
    {
        var safeModel = string.Join("/", model.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        var endpoint = BuildHuggingFaceEndpoint($"models/{safeModel}");
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _huggingFaceOptions.ApiKey);
        request.Headers.TryAddWithoutValidation("x-wait-for-model", "true");
        request.Content = JsonContent.Create(
            new HuggingFaceFeatureExtractionRequest(
                batch.Count == 1 ? batch[0] : batch,
                new HuggingFaceFeatureExtractionOptions(true)),
            options: JsonOptions);
        return request;
    }

    private Uri BuildHuggingFaceEndpoint(string relativePath)
    {
        return new Uri(new Uri(GetHuggingFaceBaseAddress()), relativePath);
    }

    private string GetHuggingFaceBaseAddress()
    {
        var baseAddress = string.IsNullOrWhiteSpace(_huggingFaceOptions.BaseAddress)
            ? "https://router.huggingface.co/hf-inference/"
            : _huggingFaceOptions.BaseAddress.Trim();
        if (!baseAddress.EndsWith("/", StringComparison.Ordinal))
        {
            baseAddress += "/";
        }

        return baseAddress;
    }

    private async Task<HttpResponseMessage> SendHuggingFaceWithRetryAsync(
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
            catch (HttpRequestException ex)
            {
                response?.Dispose();
                if (attempt < MaxGeminiRetryAttempts)
                {
                    await Task.Delay(GetRetryDelay(null, attempt), cancellationToken);
                    continue;
                }

                throw new InvalidOperationException(
                    $"Cannot connect to HuggingFace endpoint '{GetHuggingFaceBaseAddress()}'. Check DNS/network/proxy or HuggingFace:BaseAddress.",
                    ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                response?.Dispose();
                if (attempt < MaxGeminiRetryAttempts)
                {
                    await Task.Delay(GetRetryDelay(null, attempt), cancellationToken);
                    continue;
                }

                throw new InvalidOperationException(
                    $"HuggingFace embedding request timed out against '{GetHuggingFaceBaseAddress()}'. Check network or reduce benchmark size.",
                    ex);
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }

        throw new InvalidOperationException("HuggingFace embedding request could not be sent.");
    }

    private static async Task<InvalidOperationException> BuildHuggingFaceRuntimeExceptionAsync(
        string? modelName,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(modelName) ? "HuggingFace embedding" : modelName;
        var details = await ReadResponseSnippetAsync(response, cancellationToken);
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" Response: {details}";
        return (int)response.StatusCode switch
        {
            400 => new InvalidOperationException($"{name} HuggingFace rejected the embedding payload (HTTP 400). Use non-empty text and the feature-extraction task for PhoBERT.{suffix}"),
            401 or 403 => new InvalidOperationException($"{name} HuggingFace authentication failed. Check HUGGINGFACE_API_KEY or user-secrets HuggingFace:ApiKey.{suffix}"),
            404 => new InvalidOperationException($"{name} HuggingFace model or inference route was not found (HTTP 404). Check model id and HuggingFace:BaseAddress.{suffix}"),
            429 => new InvalidOperationException($"{name} HuggingFace quota/rate limit reached (HTTP 429). Wait and rerun, or reduce benchmark questions/configurations.{suffix}"),
            503 => new InvalidOperationException($"{name} HuggingFace model is loading or unavailable (HTTP 503). Rerun after a few minutes.{suffix}"),
            _ => new InvalidOperationException($"{name} HuggingFace embedding runtime returned HTTP {(int)response.StatusCode}.{suffix}")
        };
    }

    private static InvalidOperationException BuildHuggingFaceRuntimeException(
        string? modelName,
        HttpResponseMessage response)
    {
        var name = string.IsNullOrWhiteSpace(modelName) ? "HuggingFace embedding" : modelName;
        return (int)response.StatusCode switch
        {
            401 or 403 => new InvalidOperationException($"{name} HuggingFace authentication failed. Check HUGGINGFACE_API_KEY or user-secrets HuggingFace:ApiKey."),
            429 => new InvalidOperationException($"{name} đang bị giới hạn quota/rate limit HuggingFace (HTTP 429). Hãy chờ rồi chạy lại, hoặc giảm số câu hỏi/số cấu hình benchmark."),
            503 => new InvalidOperationException($"{name} HuggingFace model is loading or unavailable (HTTP 503). Hãy chạy lại sau ít phút."),
            _ => new InvalidOperationException($"{name} HuggingFace embedding runtime returned HTTP {(int)response.StatusCode}.")
        };
    }

    private static IReadOnlyList<IReadOnlyList<double>> ParseHuggingFaceEmbeddingPayload(
        JsonElement payload,
        int expectedInputs)
    {
        if (payload.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<IReadOnlyList<double>>();
        }

        var depth = GetArrayDepth(payload);
        return depth switch
        {
            1 => new[] { ReadNumberVector(payload) },
            2 => expectedInputs == 1
                ? new[] { MeanPool(ReadMatrix(payload)) }
                : payload.EnumerateArray().Select(ReadNumberVector).ToList(),
            3 => payload.EnumerateArray().Select(item => MeanPool(ReadMatrix(item))).ToList(),
            _ => Array.Empty<IReadOnlyList<double>>()
        };
    }

    private static int GetArrayDepth(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        using var enumerator = element.EnumerateArray();
        return enumerator.MoveNext() ? 1 + GetArrayDepth(enumerator.Current) : 1;
    }

    private static IReadOnlyList<IReadOnlyList<double>> ReadMatrix(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Array)
                .Select(ReadNumberVector)
                .Where(vector => vector.Count > 0)
                .ToList()
            : Array.Empty<IReadOnlyList<double>>();
    }

    private static IReadOnlyList<double> ReadNumberVector(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<double>();
        }

        var values = new List<double>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<double> MeanPool(IReadOnlyList<IReadOnlyList<double>> matrix)
    {
        var width = matrix.FirstOrDefault()?.Count ?? 0;
        if (matrix.Count == 0 || width == 0)
        {
            return Array.Empty<double>();
        }

        var sums = new double[width];
        var rows = 0;
        foreach (var row in matrix.Where(row => row.Count == width))
        {
            for (var index = 0; index < width; index++)
            {
                sums[index] += row[index];
            }

            rows++;
        }

        return rows == 0 ? Array.Empty<double>() : sums.Select(value => value / rows).ToList();
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

    private static async Task<InvalidOperationException> BuildGeminiRuntimeExceptionAsync(
        string? modelName,
        string operation,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(modelName) ? "Gemini" : modelName;
        var details = await ReadResponseSnippetAsync(response, cancellationToken);
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" Response: {details}";
        return (int)response.StatusCode switch
        {
            400 => new InvalidOperationException($"{name} Gemini {operation} runtime returned HTTP 400. Check API key, model id, non-empty input text, and outputDimensionality.{suffix}"),
            403 => new InvalidOperationException($"{name} Gemini {operation} was forbidden (HTTP 403). Check Gemini API key, project API access, billing/quota, and embedding model permission.{suffix}"),
            429 => new InvalidOperationException($"{name} Gemini {operation} quota/rate limit reached (HTTP 429). Wait and rerun, or reduce benchmark questions/configurations.{suffix}"),
            _ => new InvalidOperationException($"{name} Gemini {operation} runtime returned HTTP {(int)response.StatusCode}.{suffix}")
        };
    }

    private static async Task<string> ReadResponseSnippetAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            text = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
            return text.Length <= 700 ? text : string.Concat(text.AsSpan(0, 700), "...");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static InvalidOperationException BuildGeminiRuntimeException(
        string? modelName,
        string operation,
        HttpResponseMessage response)
    {
        var name = string.IsNullOrWhiteSpace(modelName) ? "Gemini" : modelName;
        return (int)response.StatusCode switch
        {
            403 => new InvalidOperationException($"{name} {operation} bị Gemini từ chối quyền (HTTP 403). Kiểm tra Gemini API key, project đã bật Generative Language API, billing/quota và quyền dùng model embedding."),
            429 => new InvalidOperationException($"{name} {operation} đang bị giới hạn quota/rate limit Gemini (HTTP 429). Hãy chờ 1-2 phút rồi chạy lại, hoặc giảm số câu hỏi/số chunking strategy trong lần chạy."),
            _ => new InvalidOperationException($"{name} Gemini {operation} runtime returned HTTP {(int)response.StatusCode}.")
        };
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
            var texts = SplitText(document.Text, run.ChunkingMethod ?? "Paragraph", Math.Max(200, run.ChunkSize), Math.Max(0, run.ChunkOverlap))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
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

    private static IReadOnlyList<DocumentChunk> RetrieveRelevantChunks(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyDictionary<int, double> queryEmbedding,
        IReadOnlySet<string> queryTerms)
    {
        var minimumSharedTerms = queryTerms.Count >= 4 ? 2 : 1;
        return chunks
            .Select(chunk =>
            {
                var vectorScore = CosineSimilarity(queryEmbedding, chunk.Embedding);
                var sharedTerms = CountSharedTerms(queryTerms, chunk.Text);
                var metadataTerms = CountSharedTerms(queryTerms, $"{chunk.FileName} {chunk.Subject} {chunk.Chapter}");
                var lexicalCoverage = queryTerms.Count == 0 ? 0 : sharedTerms / (double)queryTerms.Count;
                var score = Clamp01((NormalizeCosine(vectorScore) * 0.62) + (lexicalCoverage * 0.30) + (metadataTerms > 0 ? 0.08 : 0));
                return new
                {
                    Chunk = chunk,
                    Score = score,
                    SharedTerms = sharedTerms,
                    MetadataTerms = metadataTerms
                };
            })
            .Where(item => queryTerms.Count == 0 || item.SharedTerms >= minimumSharedTerms || item.Score >= 0.68)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.SharedTerms)
            .ThenByDescending(item => item.MetadataTerms)
            .Take(5)
            .Select(item => item.Chunk)
            .ToList();
    }

    private static ResearchBenchmarkResult Score(
        ResearchTestQuestion question,
        string answer,
        IReadOnlyList<DocumentChunk> retrievedChunks,
        double latencyMs)
    {
        return Score(question, answer, retrievedChunks, latencyMs, null, null, null);
    }

    private static ResearchBenchmarkResult Score(
        ResearchTestQuestion question,
        string answer,
        IReadOnlyList<DocumentChunk> retrievedChunks,
        double latencyMs,
        IReadOnlyDictionary<int, double>? answerEmbedding,
        IReadOnlyDictionary<int, double>? groundTruthEmbedding,
        IReadOnlyDictionary<int, double>? questionEmbedding)
    {
        var groundTruthTerms = ExtractTerms(question.GroundTruth);
        var answerTerms = ExtractTerms(answer);
        var contextTerms = ExtractTerms(string.Join(" ", retrievedChunks.Select(item => item.Text)));

        var answerGroundTruthOverlap = BalancedOverlap(answerTerms, groundTruthTerms);
        var answerQuestionOverlap = BalancedOverlap(answerTerms, ExtractTerms(question.Question));
        var answerGroundTruthSimilarity = answerEmbedding is not null && groundTruthEmbedding is not null
            ? NormalizeCosine(CosineSimilarity(answerEmbedding, groundTruthEmbedding))
            : answerGroundTruthOverlap;
        var answerQuestionSimilarity = answerEmbedding is not null && questionEmbedding is not null
            ? NormalizeCosine(CosineSimilarity(answerEmbedding, questionEmbedding))
            : answerQuestionOverlap;
        var answerRelevancy = Clamp01((answerGroundTruthOverlap * 0.45) + (answerGroundTruthSimilarity * 0.40) + (answerQuestionSimilarity * 0.15));

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

    private static int CountSharedTerms(IReadOnlySet<string> queryTerms, string text)
    {
        if (queryTerms.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var sourceTerms = ExtractTerms(text);
        return queryTerms.Count(queryTerm => sourceTerms.Any(sourceTerm => TermsMatch(queryTerm, sourceTerm)));
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

    private static string NormalizeQuestionForTraining(string text)
    {
        return Regex.Replace(RemoveDiacritics(text).ToLowerInvariant(), @"[^\p{L}\p{N}\s]+", " ")
            .Trim();
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

    private static Dictionary<int, double> NormalizeDenseEmbedding(IReadOnlyList<double> embedding)
    {
        var norm = Math.Sqrt(embedding.Sum(value => value * value));
        var vector = new Dictionary<int, double>(embedding.Count);
        if (norm == 0)
        {
            return vector;
        }

        for (var index = 0; index < embedding.Count; index++)
        {
            var value = embedding[index] / norm;
            if (Math.Abs(value) > 0)
            {
                vector[index] = value;
            }
        }

        return vector;
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
        [property: JsonPropertyName("outputDimensionality")] int? OutputDimensionality);

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

    private sealed record HuggingFaceFeatureExtractionRequest(
        [property: JsonPropertyName("inputs")] object Inputs,
        [property: JsonPropertyName("options")] HuggingFaceFeatureExtractionOptions Options);

    private sealed record HuggingFaceFeatureExtractionOptions(
        [property: JsonPropertyName("wait_for_model")] bool WaitForModel);

    private sealed record FineTunedRequest(
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("question")] string Question);

    private sealed record FineTunedResponse(
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record LocalFineTunedExample(string Question, string Answer);
}
