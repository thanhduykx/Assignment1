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

public sealed class FallbackEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _primary;
    private readonly IEmbeddingService _fallback;

    public FallbackEmbeddingService(IEmbeddingService primary, IEmbeddingService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<Dictionary<int, double>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.EmbedAsync(text, cancellationToken);
        }
        catch (Exception ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await _fallback.EmbedAsync(text, cancellationToken);
        }
    }

    public double CosineSimilarity(IReadOnlyDictionary<int, double> left, IReadOnlyDictionary<int, double> right)
    {
        return _fallback.CosineSimilarity(left, right);
    }

    private static bool ShouldFallback(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is InvalidOperationException or HttpRequestException or TaskCanceledException;
    }
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

public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private const int MaxGeminiRetryAttempts = 4;

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

        HttpResponseMessage response;
        try
        {
            response = await SendGeminiWithRetryAsync(
                () => CreateEmbeddingRequest(model, text),
                cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Gemini embedding request timed out. Check the API key, network connection, or increase Gemini timeout in appsettings.json.");
        }

        using (response)
        {
        if (!response.IsSuccessStatusCode)
        {
            throw BuildGeminiEmbeddingException(response);
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(JsonOptions, cancellationToken);
        if (payload?.Embedding?.Values is not { Count: > 0 } values)
        {
            throw new InvalidOperationException("Gemini embedding response did not contain vector values.");
        }

        return NormalizeDenseEmbedding(values);
        }
    }

    private HttpRequestMessage CreateEmbeddingRequest(string model, string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:embedContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiEmbeddingRequest(
                $"models/{model}",
                new GeminiEmbeddingContent([new GeminiEmbeddingPart(text)]),
                Math.Max(1, _options.EmbeddingOutputDimensionality)),
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

        throw new InvalidOperationException("Gemini embedding request could not be sent.");
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

    private static InvalidOperationException BuildGeminiEmbeddingException(HttpResponseMessage response)
    {
        return (int)response.StatusCode switch
        {
            403 => new InvalidOperationException("Gemini embedding bị từ chối quyền (HTTP 403). Kiểm tra Gemini API key, project đã bật Generative Language API, billing/quota và quyền dùng model embedding."),
            429 => new InvalidOperationException("Gemini embedding đang bị giới hạn quota/rate limit (HTTP 429). Hãy chờ 1-2 phút rồi chạy lại, hoặc giảm số câu hỏi/số tài liệu trong lần chạy."),
            _ => new InvalidOperationException($"Gemini embedding request failed with HTTP {(int)response.StatusCode}.")
        };
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
