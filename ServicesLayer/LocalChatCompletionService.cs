using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataAccessLayer;

namespace ServicesLayer;

public interface ILocalChatCompletionService
{
    Task<string> RewriteQuestionAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default);

    Task<string?> GenerateAnswerAsync(
        string question,
        string subject,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        CancellationToken cancellationToken = default);
}

public sealed class OllamaChatCompletionService : ILocalChatCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly bool _enabled;

    public OllamaChatCompletionService(HttpClient httpClient, string model, bool enabled)
    {
        _httpClient = httpClient;
        _model = string.IsNullOrWhiteSpace(model) ? "gemma4:latest" : model.Trim();
        _enabled = enabled;
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
        var rewritten = await CallChatAsync(prompt, system: null, cancellationToken);
        return string.IsNullOrWhiteSpace(rewritten) ? question : rewritten.Trim().Trim('"');
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
        return await CallChatAsync(prompt, system, cancellationToken);
    }

    private async Task<string?> CallChatAsync(
        string prompt,
        string? system,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<OllamaChatMessage>();
            if (!string.IsNullOrWhiteSpace(system))
            {
                messages.Add(new OllamaChatMessage("system", system));
            }

            messages.Add(new OllamaChatMessage("user", prompt));

            var request = new OllamaChatRequest(
                _model,
                messages,
                false,
                new OllamaChatOptions(0.2, 4096, 768));

            using var response = await _httpClient.PostAsync(
                "api/chat",
                JsonContent.Create(request, options: JsonOptions),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                JsonOptions,
                cancellationToken);

            return string.IsNullOrWhiteSpace(result?.Message?.Content) ? null : result.Message.Content.Trim();
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
            return BuildSystemPrompt(subject);
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

    private static string BuildSystemPrompt(string subject)
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

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaChatOptions Options);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaChatOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_ctx")] int NumCtx,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message);
}
