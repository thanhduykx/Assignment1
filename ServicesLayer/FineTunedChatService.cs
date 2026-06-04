using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record FineTunedChatOptions(
    bool Enabled,
    string Provider,
    string Endpoint,
    double MinimumConfidence,
    bool UseLatestResearchModel,
    string ExamplesPath);

public sealed record FineTunedChatAnswer(
    string Answer,
    double Confidence,
    string MatchedQuestion,
    string ModelName,
    bool HasDirectCitation = false);

public interface IFineTunedChatService
{
    Task<FineTunedChatAnswer?> TryAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyCollection<string>? allowedSubjects,
        CancellationToken cancellationToken = default);
}

public sealed class NullFineTunedChatService : IFineTunedChatService
{
    public static readonly NullFineTunedChatService Instance = new();

    private NullFineTunedChatService()
    {
    }

    public Task<FineTunedChatAnswer?> TryAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyCollection<string>? allowedSubjects,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<FineTunedChatAnswer?>(null);
    }
}

public sealed class FineTunedChatService : IFineTunedChatService
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of", "on", "or", "that", "the", "to",
        "who", "what", "where", "when", "why", "how",
        "va", "la", "cua", "cho", "trong", "khi", "voi", "mot", "cac", "nhung", "duoc", "tu", "theo", "nay", "do", "thi", "o",
        "ai", "gi", "nao", "hay", "biet", "ve", "khong", "da", "bao", "nhieu", "may", "hoi", "can", "noi", "mon", "hoc"
    };

    private readonly IResearchRepository? _researchRepository;
    private readonly HttpClient _httpClient;
    private readonly FineTunedChatOptions _options;

    public FineTunedChatService(
        IResearchRepository? researchRepository,
        HttpClient httpClient,
        FineTunedChatOptions options)
    {
        _researchRepository = researchRepository;
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<FineTunedChatAnswer?> TryAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyCollection<string>? allowedSubjects,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var minimumConfidence = Math.Clamp(_options.MinimumConfidence <= 0 ? 0.62 : _options.MinimumConfidence, 0, 1);
        var model = _options.UseLatestResearchModel && _researchRepository is not null
            ? await _researchRepository.GetLatestFineTunedModelAsync(cancellationToken)
            : null;
        var endpoint = string.IsNullOrWhiteSpace(model?.Endpoint) ? _options.Endpoint : model.Endpoint;
        var modelName = string.IsNullOrWhiteSpace(model?.Name) ? "local-supervised-qa" : model.Name;

        if (!string.IsNullOrWhiteSpace(endpoint)
            && !endpoint.Equals("local://supervised-qa", StringComparison.OrdinalIgnoreCase))
        {
            var externalAnswer = await TryExternalAnswerAsync(endpoint, question, subject, modelName, cancellationToken);
            return externalAnswer is not null && externalAnswer.Confidence >= minimumConfidence
                ? externalAnswer
                : null;
        }

        var examples = new List<FineTunedChatExample>();
        examples.AddRange(await LoadExamplesFromFileAsync(cancellationToken));
        if (!string.IsNullOrWhiteSpace(model?.ConfigJson))
        {
            examples.AddRange(LoadExamplesFromConfig(model.ConfigJson, subject));
        }

        var localAnswer = FindBestLocalAnswer(question, subject, allowedSubjects, examples, modelName);
        return localAnswer is not null && localAnswer.Confidence >= minimumConfidence
            ? localAnswer
            : null;
    }

    private async Task<FineTunedChatAnswer?> TryExternalAnswerAsync(
        string endpoint,
        string question,
        string? subject,
        string modelName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                endpoint,
                new FineTunedChatRequest(subject ?? string.Empty, question),
                JsonOptions,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<FineTunedChatResponse>(JsonOptions, cancellationToken);
            var answer = payload?.Answer ?? payload?.Text;
            if (string.IsNullOrWhiteSpace(answer))
            {
                return null;
            }

            return new FineTunedChatAnswer(
                answer.Trim(),
                Math.Clamp(payload?.Confidence ?? 0.7, 0, 1),
                payload?.MatchedQuestion ?? string.Empty,
                string.IsNullOrWhiteSpace(payload?.ModelName) ? modelName : payload.ModelName.Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<FineTunedChatExample>> LoadExamplesFromFileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ExamplesPath) || !File.Exists(_options.ExamplesPath))
        {
            return Array.Empty<FineTunedChatExample>();
        }

        try
        {
            await using var stream = File.OpenRead(_options.ExamplesPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseExamples(document.RootElement, null);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Array.Empty<FineTunedChatExample>();
        }
    }

    private static IReadOnlyList<FineTunedChatExample> LoadExamplesFromConfig(string configJson, string? subject)
    {
        try
        {
            using var document = JsonDocument.Parse(configJson);
            return ParseExamples(document.RootElement, subject);
        }
        catch (JsonException)
        {
            return Array.Empty<FineTunedChatExample>();
        }
    }

    private static IReadOnlyList<FineTunedChatExample> ParseExamples(JsonElement root, string? defaultSubject)
    {
        var source = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("examples", out var examplesElement) && examplesElement.ValueKind == JsonValueKind.Array
                ? examplesElement
                : default;
        if (source.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FineTunedChatExample>();
        }

        var examples = new List<FineTunedChatExample>();
        foreach (var item in source.EnumerateArray())
        {
            var status = ReadString(item, "status");
            if (!string.IsNullOrWhiteSpace(status)
                && !status.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("Ready", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("Trained", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var question = ReadString(item, "question");
            var answer = ReadString(item, "answer")
                         ?? ReadString(item, "groundTruth")
                         ?? ReadString(item, "ground_truth");
            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            {
                continue;
            }

            examples.Add(new FineTunedChatExample(
                question.Trim(),
                answer.Trim(),
                ReadString(item, "subject")?.Trim() ?? defaultSubject?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(status) ? "Approved" : status.Trim()));
        }

        return examples;
    }

    private static string? ReadString(JsonElement item, string propertyName)
    {
        return item.ValueKind == JsonValueKind.Object
               && item.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static FineTunedChatAnswer? FindBestLocalAnswer(
        string question,
        string? subject,
        IReadOnlyCollection<string>? allowedSubjects,
        IReadOnlyList<FineTunedChatExample> examples,
        string modelName)
    {
        if (examples.Count == 0)
        {
            return null;
        }

        var queryTerms = ExpandTerms(ExtractTerms(question));
        var questionCodes = ExtractCourseCodes(question);
        var allowed = allowedSubjects?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList() ?? new List<string>();

        var best = examples
            .Where(example => IsSubjectAllowed(example, subject, allowed, questionCodes))
            .Select(example =>
            {
                var exampleTerms = ExpandTerms(ExtractTerms(example.Question));
                var answerTerms = ExpandTerms(ExtractTerms(example.Answer));
                var questionScore = BalancedOverlap(queryTerms, exampleTerms);
                var answerScore = BalancedOverlap(queryTerms, answerTerms) * 0.6;
                var codeBoost = questionCodes.Count > 0 && ExampleContainsAnyCode(example, questionCodes) ? 0.18 : 0;
                var subjectBoost = !string.IsNullOrWhiteSpace(subject) && SubjectMatches(example.Subject, subject) ? 0.08 : 0;
                return new
                {
                    Example = example,
                    Score = Math.Clamp(questionScore + answerScore + codeBoost + subjectBoost, 0, 1)
                };
            })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();

        if (best is null)
        {
            return null;
        }

        return new FineTunedChatAnswer(
            best.Example.Answer,
            Math.Round(best.Score, 3),
            best.Example.Question,
            modelName);
    }

    private static bool IsSubjectAllowed(
        FineTunedChatExample example,
        string? requestedSubject,
        IReadOnlyList<string> allowedSubjects,
        IReadOnlySet<string> questionCodes)
    {
        if (!string.IsNullOrWhiteSpace(requestedSubject)
            && !string.IsNullOrWhiteSpace(example.Subject)
            && !SubjectMatches(example.Subject, requestedSubject))
        {
            return false;
        }

        if (allowedSubjects.Count > 0
            && !string.IsNullOrWhiteSpace(example.Subject)
            && !allowedSubjects.Any(allowed => SubjectMatches(example.Subject, allowed)))
        {
            return false;
        }

        if (questionCodes.Count > 0 && !ExampleContainsAnyCode(example, questionCodes))
        {
            return false;
        }

        return true;
    }

    private static bool ExampleContainsAnyCode(FineTunedChatExample example, IReadOnlySet<string> codes)
    {
        var haystack = $"{example.Subject} {example.Question} {example.Answer}";
        var exampleCodes = ExtractCourseCodes(haystack);
        return codes.Any(code => exampleCodes.Contains(code));
    }

    private static double BalancedOverlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var matches = left.Count(term => right.Any(other => TermsMatch(term, other)));
        var precision = matches / (double)right.Count;
        var recall = matches / (double)left.Count;
        return precision + recall == 0 ? 0 : (2 * precision * recall) / (precision + recall);
    }

    private static HashSet<string> ExpandTerms(HashSet<string> terms)
    {
        var expanded = new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);
        if (terms.Contains("chuan") || terms.Contains("dau") || terms.Contains("outcome"))
        {
            expanded.Add("clo");
            expanded.Add("outcomes");
            expanded.Add("learning");
        }

        if (terms.Contains("danh") || terms.Contains("gia") || terms.Contains("assessment"))
        {
            expanded.Add("exam");
            expanded.Add("quiz");
            expanded.Add("assignment");
        }

        if (terms.Contains("tin") || terms.Contains("chi") || terms.Contains("credit"))
        {
            expanded.Add("nocredit");
            expanded.Add("credits");
        }

        return expanded;
    }

    private static HashSet<string> ExtractTerms(string text)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        foreach (Match match in TokenRegex.Matches(normalized))
        {
            var token = match.Value.Trim();
            if (token.Length >= 2 && !StopWords.Contains(token))
            {
                terms.Add(token);
            }
        }

        return terms;
    }

    private static IReadOnlySet<string> ExtractCourseCodes(string text)
    {
        return Regex.Matches(text ?? string.Empty, @"\b[A-Za-z]{2,}\d{2,}\b", RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool SubjectMatches(string subject, string filter)
    {
        var normalizedSubject = (subject ?? string.Empty).Trim();
        var normalizedFilter = (filter ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedSubject) || string.IsNullOrWhiteSpace(normalizedFilter))
        {
            return false;
        }

        if (normalizedSubject.Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var subjectCode = ExtractSubjectCode(normalizedSubject);
        var filterCode = ExtractSubjectCode(normalizedFilter);
        return !string.IsNullOrWhiteSpace(subjectCode)
               && subjectCode.Equals(filterCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSubjectCode(string subject)
    {
        var trimmed = (subject ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var separatorIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        var candidate = separatorIndex > 0
            ? trimmed[..separatorIndex]
            : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? trimmed;
        return new string(candidate
                .Where(character => char.IsLetterOrDigit(character) || character is '_' or '.')
                .ToArray())
            .ToUpperInvariant();
    }

    private static bool TermsMatch(string queryTerm, string sourceTerm)
    {
        if (queryTerm.Equals(sourceTerm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (queryTerm.Length < 3 || sourceTerm.Length < 3)
        {
            return false;
        }

        return sourceTerm.StartsWith(queryTerm, StringComparison.OrdinalIgnoreCase)
               || queryTerm.StartsWith(sourceTerm, StringComparison.OrdinalIgnoreCase);
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

    private sealed record FineTunedChatExample(string Question, string Answer, string Subject, string Status);

    private sealed record FineTunedChatRequest(
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("question")] string Question);

    private sealed record FineTunedChatResponse(
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("confidence")] double? Confidence,
        [property: JsonPropertyName("matchedQuestion")] string? MatchedQuestion,
        [property: JsonPropertyName("modelName")] string? ModelName);
}
