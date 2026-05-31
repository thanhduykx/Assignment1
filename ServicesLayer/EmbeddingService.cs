using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ServicesLayer;

public interface IEmbeddingService
{
    Task<Dictionary<int, double>> EmbedAsync(string text, CancellationToken cancellationToken = default);
    double CosineSimilarity(IReadOnlyDictionary<int, double> left, IReadOnlyDictionary<int, double> right);
}

public sealed class HashingEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 512;

    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of", "on", "or", "that", "the", "to",
        "va", "la", "cua", "cho", "trong", "khi", "voi", "mot", "cac", "nhung", "duoc", "tu", "theo", "nay", "do", "thi", "o"
    };

    public Task<Dictionary<int, double>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EmbedWithHashing(text));
    }

    public Dictionary<int, double> EmbedWithHashing(string text)
    {
        var vector = new Dictionary<int, double>();
        foreach (var token in Tokenize(text))
        {
            var hash = BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(token)), 0);
            var index = (int)(hash % Dimensions);
            vector[index] = vector.GetValueOrDefault(index) + 1d;
        }

        var norm = Math.Sqrt(vector.Values.Sum(value => value * value));
        if (norm == 0)
        {
            return vector;
        }

        foreach (var key in vector.Keys.ToList())
        {
            vector[key] /= norm;
        }

        return vector;
    }

    public double CosineSimilarity(IReadOnlyDictionary<int, double> left, IReadOnlyDictionary<int, double> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var smaller = left.Count < right.Count ? left : right;
        var larger = ReferenceEquals(smaller, left) ? right : left;
        return smaller.Sum(item => larger.TryGetValue(item.Key, out var value) ? item.Value * value : 0);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        foreach (Match match in TokenRegex.Matches(normalized))
        {
            var token = match.Value.Trim();
            if (token.Length >= 2 && !StopWords.Contains(token))
            {
                yield return token;
            }
        }
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
}

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly bool _enabled;
    private readonly bool _fallbackToHashing;
    private readonly HashingEmbeddingService _fallback = new();

    public OllamaEmbeddingService(HttpClient httpClient, string model, bool enabled, bool fallbackToHashing)
    {
        _httpClient = httpClient;
        _model = model;
        _enabled = enabled;
        _fallbackToHashing = fallbackToHashing;
    }

    public async Task<Dictionary<int, double>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Dictionary<int, double>();
        }

        if (!_enabled || string.IsNullOrWhiteSpace(_model))
        {
            return await FallbackAsync(text, cancellationToken);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/embeddings",
                new OllamaEmbeddingRequest(_model, text),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return await FallbackAsync(text, cancellationToken);
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken);
            if (payload?.Embedding is not { Count: > 0 } embedding)
            {
                return await FallbackAsync(text, cancellationToken);
            }

            return NormalizeDenseEmbedding(embedding);
        }
        catch (HttpRequestException)
        {
            return await FallbackAsync(text, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await FallbackAsync(text, cancellationToken);
        }
    }

    public double CosineSimilarity(IReadOnlyDictionary<int, double> left, IReadOnlyDictionary<int, double> right)
    {
        return _fallback.CosineSimilarity(left, right);
    }

    private Task<Dictionary<int, double>> FallbackAsync(string text, CancellationToken cancellationToken)
    {
        return _fallbackToHashing
            ? _fallback.EmbedAsync(text, cancellationToken)
            : Task.FromResult(new Dictionary<int, double>());
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

    private sealed record OllamaEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] List<double> Embedding);
}

public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiApiOptions _options;

    public GeminiEmbeddingService(HttpClient httpClient, GeminiApiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<Dictionary<int, double>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Dictionary<int, double>();
        }

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is required for embeddings.");
        }

        var model = string.IsNullOrWhiteSpace(_options.EmbeddingModel)
            ? "gemini-embedding-001"
            : _options.EmbeddingModel.Trim();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:embedContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiEmbeddingRequest(
                $"models/{model}",
                new GeminiEmbeddingContent([new GeminiEmbeddingPart(text)]),
                Math.Max(1, _options.EmbeddingOutputDimensionality)),
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini embedding request failed with HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(JsonOptions, cancellationToken);
        if (payload?.Embedding?.Values is not { Count: > 0 } values)
        {
            throw new InvalidOperationException("Gemini embedding response did not contain vector values.");
        }

        return NormalizeDenseEmbedding(values);
    }

    public double CosineSimilarity(IReadOnlyDictionary<int, double> left, IReadOnlyDictionary<int, double> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var smaller = left.Count < right.Count ? left : right;
        var larger = ReferenceEquals(smaller, left) ? right : left;
        return smaller.Sum(item => larger.TryGetValue(item.Key, out var value) ? item.Value * value : 0);
    }

    internal static Dictionary<int, double> NormalizeDenseEmbedding(IReadOnlyList<double> embedding)
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

    private sealed record GeminiEmbedding(
        [property: JsonPropertyName("values")] List<double>? Values);
}
