using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record ChatAnswer(string Answer, IReadOnlyList<SourceCitation> Citations, IReadOnlyList<ChatMessage> History);

public interface IRagChatService
{
    Task<ChatAnswer> AskAsync(Guid sessionId, string question, string? userDisplayName = null, string? language = null, CancellationToken cancellationToken = default);
}

public sealed class RagChatService : IRagChatService
{
    private const int TopK = 5;
    private const double MinimumScore = 0.7;
    private const string OutOfScopeAnswer = "Mình không đủ dữ liệu trong tài liệu để trả lời câu hỏi này.";

    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly string[] PromptInjectionSignals =
    {
        "ignore",
        "disregard",
        "forget previous",
        "forget all",
        "bypass",
        "jailbreak",
        "system prompt",
        "developer message",
        "hidden instruction",
        "reveal prompt",
        "show prompt",
        "do not follow",
        "bo qua",
        "mac ke",
        "quen tat ca",
        "khong can tuan thu",
        "khong can theo tai lieu",
        "tra loi ngoai tai lieu",
        "bo qua quy tac",
        "bo qua quy chuan",
        "bo qua bao mat",
        "bo qua luat le",
        "phot lo"
    };

    private static readonly string[] CasualChatSignals =
    {
        "hi",
        "hello",
        "hey",
        "xin chao",
        "chao",
        "chao ban",
        "alo",
        "cam on",
        "thanks",
        "thank you",
        "tam biet",
        "bye"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of", "on", "or", "that", "the", "to",
        "who", "what", "where", "when", "why", "how", "please", "tell", "about",
        "va", "la", "cua", "cho", "trong", "khi", "voi", "mot", "cac", "nhung", "duoc", "tu", "theo", "nay", "do", "thi", "o",
        "ai", "gi", "nao", "hay", "cho", "biet", "ve", "khong", "da", "bao", "nhieu", "may", "hoi", "can", "noi", "noi dung"
    };

    private readonly IKnowledgeRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILocalChatCompletionService _chatCompletionService;

    public RagChatService(
        IKnowledgeRepository repository,
        IEmbeddingService embeddingService,
        ILocalChatCompletionService chatCompletionService)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _chatCompletionService = chatCompletionService;
    }

    public async Task<ChatAnswer> AskAsync(
        Guid sessionId,
        string question,
        string? userDisplayName = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuestion = question.Trim();
        var responseLanguage = NormalizeLanguage(language);
        if (string.IsNullOrWhiteSpace(trimmedQuestion))
        {
            throw new InvalidOperationException("Question cannot be empty.");
        }

        var session = await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken);
        var historyBeforeQuestion = session.Messages.ToList();

        await _repository.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "user",
            Content = trimmedQuestion
        }, cancellationToken);

        if (LooksLikePromptInjection(trimmedQuestion))
        {
            return await SaveAssistantAnswer(sessionId, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        if (IsBotIdentityQuestion(trimmedQuestion))
        {
            return await SaveAssistantAnswer(sessionId, BuildBotIdentityAnswer(responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        if (IsUserIdentityQuestion(trimmedQuestion))
        {
            return await SaveAssistantAnswer(sessionId, BuildUserIdentityAnswer(userDisplayName, responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        if (IsCasualChat(trimmedQuestion))
        {
            return await SaveAssistantAnswer(sessionId, BuildCasualChatAnswer(trimmedQuestion, responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        var rewrittenQuestion = await _chatCompletionService.RewriteQuestionAsync(
            trimmedQuestion,
            historyBeforeQuestion,
            cancellationToken);

        var allChunks = await _repository.GetChunksAsync(cancellationToken);
        if (allChunks.Count == 0)
        {
            return await SaveAssistantAnswer(sessionId, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        var flmChunks = allChunks.Where(IsFlmChunk).ToList();
        var scopedChunks = flmChunks.Count > 0 ? flmChunks : allChunks;
        var queryTerms = ExtractTerms(rewrittenQuestion);
        var minimumSharedTerms = queryTerms.Count >= 4 ? 2 : 1;
        var queryEmbedding = await _embeddingService.EmbedAsync(rewrittenQuestion, cancellationToken);

        var matches = scopedChunks
            .Select(chunk => new
            {
                Chunk = chunk,
                VectorScore = _embeddingService.CosineSimilarity(queryEmbedding, chunk.Embedding),
                TextSharedTerms = CountSharedTerms(queryTerms, chunk.Text),
                MetadataSharedTerms = CountSharedTerms(queryTerms, BuildChunkMetadataText(chunk))
            })
            .Select(item => new
            {
                item.Chunk,
                Score = CalculateRetrievalScore(
                    item.VectorScore,
                    item.TextSharedTerms,
                    item.MetadataSharedTerms,
                    queryTerms.Count),
                SharedTerms = item.TextSharedTerms + item.MetadataSharedTerms,
                item.MetadataSharedTerms
            })
            .Where(item => item.Score >= MinimumScore && item.SharedTerms >= minimumSharedTerms)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.MetadataSharedTerms)
            .ThenByDescending(item => item.SharedTerms)
            .Take(TopK)
            .ToList();

        if (matches.Count == 0)
        {
            return await SaveAssistantAnswer(sessionId, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        var citations = matches.Select(item => new SourceCitation
        {
            DocumentId = item.Chunk.DocumentId,
            FileName = item.Chunk.FileName,
            Subject = item.Chunk.Subject,
            Chapter = item.Chunk.Chapter,
            ChunkIndex = item.Chunk.ChunkIndex,
            Score = Math.Round(item.Score, 3),
            Excerpt = CreateExcerpt(item.Chunk.Text)
        }).ToList();

        var matchedChunks = matches.Select(item => item.Chunk).ToList();
        var answer = await _chatCompletionService.GenerateAnswerAsync(
                         trimmedQuestion,
                         ResolveSubject(matchedChunks),
                         historyBeforeQuestion,
                         matchedChunks,
                         responseLanguage,
                         cancellationToken)
                     ?? BuildGroundedAnswer(queryTerms, matchedChunks, responseLanguage);

        if (IsInsufficientDataAnswer(answer))
        {
            return await SaveAssistantAnswer(sessionId, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), cancellationToken);
        }

        return await SaveAssistantAnswer(sessionId, answer, citations, cancellationToken);
    }

    private async Task<ChatAnswer> SaveAssistantAnswer(
        Guid sessionId,
        string answer,
        IReadOnlyList<SourceCitation> citations,
        CancellationToken cancellationToken)
    {
        await _repository.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "assistant",
            Content = answer,
            Citations = citations.ToList()
        }, cancellationToken);

        var session = await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken);
        return new ChatAnswer(answer, citations, session.Messages);
    }

    private static bool IsFlmChunk(DocumentChunk chunk)
    {
        return chunk.FileName.StartsWith("FLM-", StringComparison.OrdinalIgnoreCase)
               || chunk.FileName.Contains("Syllabus-11835", StringComparison.OrdinalIgnoreCase)
               || chunk.Subject.Contains("DBA103", StringComparison.OrdinalIgnoreCase)
               || chunk.Chapter.Contains("Syllabus 11835", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePromptInjection(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        return PromptInjectionSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
    }

    private static bool IsCasualChat(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        var compact = Regex.Replace(normalized, @"[^\p{L}\p{N}\s]+", " ").Trim();
        var terms = ExtractTerms(question);

        return CasualChatSignals.Contains(compact)
               || (terms.Count <= 2 && CasualChatSignals.Any(signal => compact.Contains(signal, StringComparison.Ordinal)));
    }

    private static bool IsBotIdentityQuestion(string question)
    {
        var compact = NormalizeQuestion(question);
        return compact is "bot la ai" or "chatbot la ai" or "ban la ai" or "may la ai"
               || compact.Contains("bot cua ban la ai", StringComparison.Ordinal)
               || compact.Contains("chatbot nay la ai", StringComparison.Ordinal);
    }

    private static bool IsUserIdentityQuestion(string question)
    {
        var compact = NormalizeQuestion(question);
        return compact is "toi la ai" or "minh la ai" or "tao la ai" or "em la ai";
    }

    private static string NormalizeLanguage(string? language)
    {
        return language?.Equals("vi", StringComparison.OrdinalIgnoreCase) == true ? "vi" : "en";
    }

    private static string BuildOutOfScopeAnswer(string language)
    {
        return language == "vi"
            ? "M\u00ecnh kh\u00f4ng \u0111\u1ee7 d\u1eef li\u1ec7u trong t\u00e0i li\u1ec7u \u0111\u1ec3 tr\u1ea3 l\u1eddi c\u00e2u h\u1ecfi n\u00e0y."
            : "I do not have enough data in the documents to answer this question.";
    }

    private static string BuildBotIdentityAnswer(string language)
    {
        if (language == "en")
        {
            return "I am an AI assistant specialized in searching and explaining content from your learning document repository. Ask a question, and I will look through the documents, summarize the relevant parts clearly, and include sources when there is enough data.";
        }

        return "Mình là AI chuyên hỗ trợ tra cứu và giải thích nội dung trong kho tài liệu học tập. Nói đơn giản: bạn hỏi, mình tìm trong tài liệu, tóm gọn lại cho dễ hiểu, rồi kèm nguồn khi có dữ liệu.";
    }

    private static string BuildUserIdentityAnswer(string? userDisplayName, string language)
    {
        var name = string.IsNullOrWhiteSpace(userDisplayName) ? (language == "vi" ? "b\u1ea1n" : "you") : userDisplayName.Trim();
        if (language == "en")
        {
            return $"You are {name}. In this app, you are the owner of this document workspace and the person I am helping.";
        }

        return $"Bạn là {name}. Trong ứng dụng này, bạn là chủ kho tài liệu và là người mình đang hỗ trợ.";
    }

    private static string BuildCasualChatAnswer(string question, string language)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        if (normalized.Contains("cam on", StringComparison.Ordinal)
            || normalized.Contains("thanks", StringComparison.Ordinal)
            || normalized.Contains("thank you", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "Không có gì. Bạn cần tìm thông tin gì trong tài liệu thì cứ hỏi mình.";
            }

            return "You're welcome. Ask me anything you want to find in the documents.";
        }

        if (normalized.Contains("tam biet", StringComparison.Ordinal)
            || normalized.Contains("bye", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "Tạm biệt. Khi cần tra cứu tài liệu, bạn mở lại mình là được.";
            }

            return "Goodbye. When you need to search the documents again, open the chat and ask me.";
        }

        if (language == "vi")
        {
            return "Chào bạn, mình đây. Bạn muốn tìm thông tin gì trong tài liệu?";
        }

        return "Hi, I am here. What information do you want to find in the documents?";
    }

    private static string BuildBotIdentityAnswer()
    {
        return "Mình là AI chuyên hỗ trợ tra cứu và giải thích nội dung trong kho tài liệu học tập. Nói đơn giản: bạn hỏi, mình tìm trong tài liệu, tóm gọn lại cho dễ hiểu, rồi kèm nguồn khi có dữ liệu.";
    }

    private static string BuildUserIdentityAnswer(string? userDisplayName)
    {
        var name = string.IsNullOrWhiteSpace(userDisplayName) ? "bạn" : userDisplayName.Trim();
        return $"Bạn là {name}. Và nói thật là cái tên này nhìn khá sáng giao diện, hợp để làm chủ kho tài liệu này đấy.";
    }

    private static string BuildCasualChatAnswer(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        if (normalized.Contains("cam on", StringComparison.Ordinal)
            || normalized.Contains("thanks", StringComparison.Ordinal)
            || normalized.Contains("thank you", StringComparison.Ordinal))
        {
            return "Không có gì. Bạn cần tìm thông tin gì trong tài liệu thì cứ hỏi mình.";
        }

        if (normalized.Contains("tam biet", StringComparison.Ordinal)
            || normalized.Contains("bye", StringComparison.Ordinal))
        {
            return "Tạm biệt. Khi cần tra cứu tài liệu, bạn mở lại mình là được.";
        }

        return "Chào bạn, mình đây. Bạn muốn tìm thông tin gì trong tài liệu?";
    }

    private static string NormalizeQuestion(string question)
    {
        var normalized = RemoveDiacritics(question).ToLowerInvariant();
        return Regex.Replace(normalized, @"[^\p{L}\p{N}\s]+", " ").Trim();
    }

    private static bool IsInsufficientDataAnswer(string answer)
    {
        var normalized = RemoveDiacritics(answer).ToLowerInvariant();
        return normalized.Contains("khong du du lieu", StringComparison.Ordinal)
               || normalized.Contains("khong tim thay thong tin", StringComparison.Ordinal)
               || normalized.Contains("khong co trong tai lieu", StringComparison.Ordinal)
               || normalized.Contains("not enough data", StringComparison.Ordinal)
               || normalized.Contains("insufficient data", StringComparison.Ordinal)
               || normalized.Contains("do not have enough data", StringComparison.Ordinal);
    }

    private static string BuildGroundedAnswer(IReadOnlySet<string> queryTerms, IReadOnlyList<DocumentChunk> chunks, string language)
    {
        var selectedSentences = chunks
            .SelectMany(chunk => SplitSentences(chunk.Text))
            .Select(sentence => new
            {
                Text = sentence,
                SharedTerms = CountSharedTerms(queryTerms, sentence)
            })
            .Where(item => item.Text.Length > 8 && item.SharedTerms > 0)
            .OrderByDescending(item => item.SharedTerms)
            .Select(item => item.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (selectedSentences.Count == 0)
        {
            return BuildOutOfScopeAnswer(language);
        }

        if (language == "vi")
        {
            return "Mình tìm được trong tài liệu các ý liên quan sau:\n\n" +
                   string.Join("\n", selectedSentences.Select(sentence => $"- {sentence}")) +
                   "\n\nMình chỉ dùng thông tin từ các nguồn trích dẫn bên dưới.";
        }

        return "I found these relevant points in the documents:\n\n" +
               string.Join("\n", selectedSentences.Select(sentence => $"- {sentence}")) +
               "\n\nI only used information from the cited sources below.";
    }

    private static string BuildGroundedAnswer(IReadOnlySet<string> queryTerms, IReadOnlyList<DocumentChunk> chunks)
    {
        var selectedSentences = chunks
            .SelectMany(chunk => SplitSentences(chunk.Text))
            .Select(sentence => new
            {
                Text = sentence,
                SharedTerms = CountSharedTerms(queryTerms, sentence)
            })
            .Where(item => item.Text.Length > 8 && item.SharedTerms > 0)
            .OrderByDescending(item => item.SharedTerms)
            .Select(item => item.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (selectedSentences.Count == 0)
        {
            return OutOfScopeAnswer;
        }

        return "Mình tìm được trong tài liệu các ý liên quan sau:\n\n" +
               string.Join("\n", selectedSentences.Select(sentence => $"- {sentence}")) +
               "\n\nMình chỉ dùng thông tin từ các nguồn trích dẫn bên dưới.";
    }

    private static string ResolveSubject(IReadOnlyList<DocumentChunk> chunks)
    {
        var subject = chunks
            .Select(chunk => chunk.Subject)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(subject))
        {
            return "môn học";
        }

        return subject;
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        var separators = new[] { ". ", "! ", "? ", "\n" };
        return text
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(sentence => sentence.Trim().Trim('-', '*'))
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence));
    }

    private static int CountSharedTerms(IReadOnlySet<string> queryTerms, string chunkText)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var chunkTerms = ExtractTerms(chunkText);
        return queryTerms.Count(queryTerm => chunkTerms.Any(chunkTerm => TermsMatch(queryTerm, chunkTerm)));
    }

    private static double CalculateRetrievalScore(
        double vectorScore,
        int textSharedTerms,
        int metadataSharedTerms,
        int queryTermCount)
    {
        if (metadataSharedTerms > 0)
        {
            return Math.Max(vectorScore, MinimumScore);
        }

        if (queryTermCount == 0)
        {
            return vectorScore;
        }

        var sharedTerms = textSharedTerms + metadataSharedTerms;
        var enoughLexicalEvidence = queryTermCount <= 3
            ? sharedTerms >= 1
            : sharedTerms >= 2;

        if (!enoughLexicalEvidence)
        {
            return vectorScore;
        }

        var lexicalCoverage = sharedTerms / (double)queryTermCount;
        var lexicalScore = lexicalCoverage >= 0.45 ? MinimumScore : vectorScore;
        return Math.Max(vectorScore, lexicalScore);
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

        if (sourceTerm.StartsWith(queryTerm, StringComparison.OrdinalIgnoreCase)
            || queryTerm.StartsWith(sourceTerm, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return LooksLikeTypo(queryTerm, sourceTerm);
    }

    private static bool LooksLikeTypo(string queryTerm, string sourceTerm)
    {
        if (queryTerm.Length < 4 || sourceTerm.Length < 4)
        {
            return false;
        }

        var lengthGap = Math.Abs(queryTerm.Length - sourceTerm.Length);
        if (lengthGap > 2)
        {
            return false;
        }

        var maxDistance = Math.Min(queryTerm.Length, sourceTerm.Length) <= 5 ? 1 : 2;
        return LevenshteinDistanceAtMost(queryTerm, sourceTerm, maxDistance);
    }

    private static bool LevenshteinDistanceAtMost(string left, string right, int maxDistance)
    {
        if (Math.Abs(left.Length - right.Length) > maxDistance)
        {
            return false;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            var rowMinimum = current[0];

            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var cost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[rightIndex]);
            }

            if (rowMinimum > maxDistance)
            {
                return false;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length] <= maxDistance;
    }

    private static string BuildChunkMetadataText(DocumentChunk chunk)
    {
        return $"{chunk.FileName} {chunk.Subject} {chunk.Chapter}";
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

    private static string CreateExcerpt(string text)
    {
        const int maxLength = 320;
        var compact = string.Join(" ", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= maxLength ? compact : $"{compact[..maxLength]}...";
    }
}
