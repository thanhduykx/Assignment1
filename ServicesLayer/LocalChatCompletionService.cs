using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record ChatChunkRerankResult(int CandidateNumber, double Score, string Reason);

public interface ILocalChatCompletionService
{
    Task<string> RewriteQuestionAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatChunkRerankResult>> RerankChunksAsync(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        CancellationToken cancellationToken = default);

    Task<string?> GenerateAnswerAsync(
        string question,
        string subject,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        CancellationToken cancellationToken = default);
}

public sealed class GeminiChatCompletionService : ILocalChatCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly bool _enabled;

    public GeminiChatCompletionService(HttpClient httpClient, string model, string? apiKey, bool enabled)
    {
        _httpClient = httpClient;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-3.5-flash" : model.Trim();
        _apiKey = apiKey?.Trim() ?? string.Empty;
        _enabled = enabled && !string.IsNullOrWhiteSpace(_apiKey);
    }

    public async Task<string> RewriteQuestionAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(question) || history.Count == 0)
        {
            return question;
        }

        var prompt = BuildRewritePrompt(question, history);
        var rewritten = await CallGenerateContentAsync(prompt, system: null, cancellationToken);
        return string.IsNullOrWhiteSpace(rewritten) ? question : rewritten.Trim().Trim('"');
    }

    public async Task<IReadOnlyList<ChatChunkRerankResult>> RerankChunksAsync(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(question) || chunks.Count == 0)
        {
            return Array.Empty<ChatChunkRerankResult>();
        }

        var system = """
            You are a strict retrieval quality judge for a document-grounded chatbot.
            Do not answer the user's question.
            Select only chunks that directly contain evidence needed to answer the question.
            If a chunk is merely about the same course or topic but does not answer the question, do not select it.
            Return only valid JSON.
            """;
        var prompt = BuildRerankPrompt(question, chunks, language);
        var response = await CallGenerateContentAsync(prompt, system, cancellationToken);
        return ParseRerankResponse(response, chunks.Count);
    }

    public async Task<string?> GenerateAnswerAsync(
        string question,
        string subject,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(question) || chunks.Count == 0)
        {
            return null;
        }

        var prompt = BuildAnswerPrompt(question, history, chunks, language);
        var system = BuildSystemPrompt(subject, language);
        return await CallGenerateContentAsync(prompt, system, cancellationToken);
    }

    private static string BuildRerankPrompt(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Choose the best evidence chunks for this question.");
        builder.AppendLine("Return JSON only in this shape:");
        builder.AppendLine("""{"selected":[{"candidate":1,"score":0.95,"reason":"direct evidence"}]}""");
        builder.AppendLine("Rules:");
        builder.AppendLine("- candidate is the candidate number shown below.");
        builder.AppendLine("- score is 0.0 to 1.0.");
        builder.AppendLine("- Select at most 6 candidates.");
        builder.AppendLine("- Select nothing if none of the candidates directly support an answer.");
        builder.AppendLine("- Prefer exact facts, numbers, definitions, requirements, schedules, and assessment details.");
        builder.AppendLine($"Answer language later will be: {language}.");
        builder.AppendLine();
        builder.AppendLine("Question:");
        builder.AppendLine(question.Trim());
        builder.AppendLine();
        builder.AppendLine("Candidates:");

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"Candidate {index + 1}");
            builder.AppendLine($"File: {chunk.FileName}");
            builder.AppendLine($"Subject: {chunk.Subject}");
            builder.AppendLine($"Chapter: {chunk.Chapter}");
            builder.AppendLine($"Source chunk: {chunk.ChunkIndex}");
            builder.AppendLine("Text:");
            builder.AppendLine(TrimForPrompt(chunk.Text, 900));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<ChatChunkRerankResult> ParseRerankResponse(string? response, int candidateCount)
    {
        if (string.IsNullOrWhiteSpace(response) || candidateCount <= 0)
        {
            return Array.Empty<ChatChunkRerankResult>();
        }

        var json = ExtractJsonObject(response);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<ChatChunkRerankResult>();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<GeminiRerankResponse>(json, JsonOptions);
            return payload?.Selected is null
                ? Array.Empty<ChatChunkRerankResult>()
                : payload.Selected
                    .Where(item => item.Candidate >= 1 && item.Candidate <= candidateCount)
                    .Select(item => new ChatChunkRerankResult(
                        item.Candidate,
                        Math.Clamp(item.Score, 0, 1),
                        item.Reason ?? string.Empty))
                    .GroupBy(item => item.CandidateNumber)
                    .Select(group => group.OrderByDescending(item => item.Score).First())
                    .OrderByDescending(item => item.Score)
                    .Take(6)
                    .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<ChatChunkRerankResult>();
        }
    }

    private static string ExtractJsonObject(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = Regex.Replace(trimmed, @"^```(?:json)?\s*|\s*```$", string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : string.Empty;
    }

    private static string TrimForPrompt(string text, int maxLength)
    {
        var compact = string.Join(" ", (text ?? string.Empty).Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= maxLength ? compact : compact[..maxLength] + "...";
    }

    private async Task<string?> CallGenerateContentAsync(
        string prompt,
        string? system,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"v1beta/models/{_model}:generateContent");

            request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
            request.Content = JsonContent.Create(
                new GeminiGenerateContentRequest(
                    string.IsNullOrWhiteSpace(system)
                        ? null
                        : new GeminiContent(null, [new GeminiPart(system)]),
                    [new GeminiContent("user", [new GeminiPart(prompt)])],
                    new GeminiGenerationConfig(0.2, 1024)),
                options: JsonOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(
                JsonOptions,
                cancellationToken);

            var text = result?.Candidates?
                .FirstOrDefault()?
                .Content?
                .Parts?
                .Select(part => part.Text)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
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

    private static string BuildSystemPrompt(string subject, string language)
    {
        if (language != "en")
        {
            return ChatPromptBuilder.BuildVietnameseSystemPrompt(subject);
        }

        var subjectName = string.IsNullOrWhiteSpace(subject) ? "the course" : subject.Trim();
        return $"""
            You are a learning assistant for {subjectName}.
            Answer naturally and clearly, but only use information from the provided Documents section.
            Do not add outside knowledge, facts, definitions, numbers, rules, or conclusions.
            If the documents do not contain enough data to answer directly, reply with exactly:
            "I do not have enough data in the documents to answer this question."
            Answer in English and keep it concise.
            """;
    }

    private static string BuildRewritePrompt(string question, IReadOnlyList<ChatMessage> history)
    {
        return $"""
            Dựa vào lịch sử hội thoại, hãy viết lại câu hỏi thành câu hỏi độc lập đầy đủ ý nghĩa.
            Không dùng đại từ mơ hồ như "nó", "cái đó", "phần này", "ở trên".
            Nếu câu hỏi đã rõ, giữ nguyên.
            Chỉ trả về câu hỏi đã viết lại, không giải thích.

            Lịch sử:
            {BuildHistoryText(history)}

            Câu hỏi gốc: {question.Trim()}
            Câu hỏi viết lại:
            """;
    }

    private static string BuildAnswerPrompt(
        string question,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        if (language != "en")
        {
            return BuildAnswerPrompt(question, history, chunks);
        }

        var builder = new StringBuilder();
        builder.AppendLine("Documents to use for answering:");

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"[{index + 1}] {chunk.FileName} / {chunk.Subject} / {chunk.Chapter} / chunk {chunk.ChunkIndex}:");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Conversation history:");
        builder.AppendLine(BuildHistoryText(history));
        builder.AppendLine();
        builder.AppendLine("Answer requirements:");
        builder.AppendLine("- Prefer a natural answer, like a chatbot talking to a student.");
        builder.AppendLine("- Only answer the parts clearly supported by the documents.");
        builder.AppendLine("- If the chunks above are only loosely related or not enough to answer directly, reply: \"I do not have enough data in the documents to answer this question.\"");
        builder.AppendLine("- Answer in English.");
        builder.AppendLine();
        builder.AppendLine("Student question:");
        builder.AppendLine(question.Trim());

        return builder.ToString();
    }

    private static string BuildAnswerPrompt(
        string question,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Tài liệu dùng để trả lời:");

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"[{index + 1}] {chunk.FileName} / {chunk.Subject} / {chunk.Chapter} / chunk {chunk.ChunkIndex}:");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Lịch sử hội thoại:");
        builder.AppendLine(BuildHistoryText(history));
        builder.AppendLine();
        builder.AppendLine("Yêu cầu trả lời:");
        builder.AppendLine("- Ưu tiên trả lời tự nhiên như một chatbot đang trò chuyện với sinh viên.");
        builder.AppendLine("- Chỉ trả lời phần có căn cứ rõ trong tài liệu.");
        builder.AppendLine("- Nếu các đoạn tài liệu bên trên chỉ liên quan lỏng lẻo hoặc không đủ để trả lời trực tiếp, trả lời: \"Mình không đủ dữ liệu trong tài liệu để trả lời câu hỏi này.\"");
        builder.AppendLine();
        builder.AppendLine("Câu hỏi của sinh viên:");
        builder.AppendLine(question.Trim());

        return builder.ToString();
    }

    private static string BuildHistoryText(IReadOnlyList<ChatMessage> history)
    {
        if (history.Count == 0)
        {
            return "(không có)";
        }

        return string.Join(
            "\n",
            history
                .TakeLast(6)
                .Select(message =>
                    $"{(message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "Sinh viên" : "Trợ lý")}: {message.Content}"));
    }

    private sealed record GeminiGenerateContentRequest(
        [property: JsonPropertyName("systemInstruction")] GeminiContent? SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiContent(
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record GeminiGenerationConfig(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);

    private sealed record GeminiRerankResponse(
        [property: JsonPropertyName("selected")] IReadOnlyList<GeminiRerankItem>? Selected);

    private sealed record GeminiRerankItem(
        [property: JsonPropertyName("candidate")] int Candidate,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("reason")] string? Reason);

    private sealed record GeminiGenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content);
}

internal static class ChatPromptBuilder
{
    public static string BuildVietnameseSystemPrompt(string subject)
    {
        var subjectName = string.IsNullOrWhiteSpace(subject) ? "môn học" : subject.Trim();
        return $"""
            Bạn là chatbot hỗ trợ sinh viên môn {subjectName}.
            Hãy trả lời tự nhiên, thân thiện, dễ hiểu, nhưng chỉ dùng thông tin có trong phần Tài liệu được cung cấp.
            Không dùng kiến thức ngoài tài liệu để bổ sung sự kiện, định nghĩa, số liệu, quy định hoặc kết luận.
            Nếu tài liệu không đủ dữ liệu để trả lời trực tiếp câu hỏi, chỉ trả lời đúng câu:
            "Mình không đủ dữ liệu trong tài liệu để trả lời câu hỏi này."
            Trả lời bằng tiếng Việt, ngắn gọn, rõ ý; có thể diễn giải lại nội dung tài liệu thay vì chép nguyên văn.
            """;
    }
}
