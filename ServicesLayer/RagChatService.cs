using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DataAccessLayer;

namespace ServicesLayer;

public sealed record ChatAnswer(
    string Answer,
    IReadOnlyList<SourceCitation> Citations,
    IReadOnlyList<ChatMessage> History,
    string? ResolvedSubject = null,
    bool NeedsClarification = false,
    IReadOnlyList<string>? SubjectOptions = null);

public interface IRagChatService
{
    Task<ChatAnswer> AskAsync(
        Guid sessionId,
        string question,
        string? userDisplayName = null,
        string? subjectFilter = null,
        string? language = null,
        IReadOnlyCollection<string>? allowedSubjects = null,
        ChatSessionOwnerInfo? ownerInfo = null,
        CancellationToken cancellationToken = default);
}

public sealed class RagChatService : IRagChatService
{
    private const int TopK = 5;
    private const int RerankCandidateK = 12;
    private const int MaxBatchQuestions = 50;
    private const double MinimumScore = 0.7;
    private const double MinimumAnswerGroundingRatio = 0.42;
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

    private static readonly HashSet<string> AnswerScaffoldTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "answer", "based", "below", "citation", "cited", "course", "data", "document", "documents", "found", "from", "information",
        "student", "source", "sources",
        "ban", "cau", "day", "du", "duoi", "hoi", "lieu", "minh", "nguon", "noi", "sau", "sinh", "tai", "theo", "thong", "tin",
        "toi", "tra", "trich", "vien"
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
        string? subjectFilter = null,
        string? language = null,
        IReadOnlyCollection<string>? allowedSubjects = null,
        ChatSessionOwnerInfo? ownerInfo = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuestion = question.Trim();
        var responseLanguage = NormalizeLanguage(language);
        if (string.IsNullOrWhiteSpace(trimmedQuestion))
        {
            throw new InvalidOperationException("Question cannot be empty.");
        }

        var session = await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken, ownerInfo);
        var historyBeforeQuestion = session.Messages.ToList();

        await _repository.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "user",
            Content = trimmedQuestion
        }, cancellationToken, ownerInfo);

        if (LooksLikePromptInjection(trimmedQuestion))
        {
            return await SaveAssistantAnswer(sessionId, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), cancellationToken, ownerInfo);
        }

        var questionBatch = SplitQuestionBatch(trimmedQuestion);
        if (questionBatch.Count > 1)
        {
            var scopedChunks = await GetScopedChunksAsync(allowedSubjects, cancellationToken);
            var questionsToAnswer = questionBatch.Take(MaxBatchQuestions).ToList();
            var answers = new List<SingleQuestionAnswer>(questionsToAnswer.Count);

            foreach (var batchQuestion in questionsToAnswer)
            {
                answers.Add(await BuildSingleQuestionAnswerAsync(
                    batchQuestion,
                    historyBeforeQuestion,
                    userDisplayName,
                    responseLanguage,
                    subjectFilter,
                    allowedSubjects,
                    scopedChunks,
                    cancellationToken));
            }

            var answerText = FormatBatchAnswer(answers, questionBatch.Count - questionsToAnswer.Count, responseLanguage);
            var citations = MergeCitations(answers.SelectMany(item => item.Citations));
            return await SaveAssistantAnswer(sessionId, answerText, citations, cancellationToken, ownerInfo);
        }

        var singleAnswer = await BuildSingleQuestionAnswerAsync(
            trimmedQuestion,
            historyBeforeQuestion,
            userDisplayName,
            responseLanguage,
            subjectFilter,
            allowedSubjects,
            scopedChunks: null,
            cancellationToken);

        return await SaveAssistantAnswer(
            sessionId,
            singleAnswer.Answer,
            singleAnswer.Citations,
            cancellationToken,
            ownerInfo,
            singleAnswer.ResolvedSubject,
            singleAnswer.NeedsClarification,
            singleAnswer.SubjectOptions);
    }

    private async Task<SingleQuestionAnswer> BuildSingleQuestionAnswerAsync(
        string question,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        string? userDisplayName,
        string responseLanguage,
        string? subjectFilter,
        IReadOnlyCollection<string>? allowedSubjects,
        IReadOnlyList<DocumentChunk>? scopedChunks,
        CancellationToken cancellationToken)
    {
        if (IsBotIdentityQuestion(question))
        {
            return new SingleQuestionAnswer(question, BuildBotIdentityAnswer(responseLanguage), Array.Empty<SourceCitation>());
        }

        if (IsUserIdentityQuestion(question))
        {
            return new SingleQuestionAnswer(question, BuildUserIdentityAnswer(userDisplayName, responseLanguage), Array.Empty<SourceCitation>());
        }

        if (IsCasualChat(question))
        {
            return new SingleQuestionAnswer(question, BuildCasualChatAnswer(question, responseLanguage), Array.Empty<SourceCitation>());
        }

        var rewrittenQuestion = await _chatCompletionService.RewriteQuestionAsync(
            question,
            historyBeforeQuestion,
            cancellationToken);
        var resolvedQuestion = rewrittenQuestion;

        scopedChunks ??= await GetScopedChunksAsync(allowedSubjects, cancellationToken);
        if (scopedChunks.Count == 0)
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>());
        }

        var route = ResolveSubjectRoute(resolvedQuestion, subjectFilter, scopedChunks);
        if (route.NeedsClarification)
        {
            return new SingleQuestionAnswer(
                question,
                BuildSubjectClarificationAnswer(route.CandidateSubjects, responseLanguage),
                Array.Empty<SourceCitation>(),
                null,
                true,
                route.CandidateSubjects);
        }

        if (!string.IsNullOrWhiteSpace(route.SelectedSubject))
        {
            scopedChunks = scopedChunks
                .Where(chunk => SubjectMatches(chunk.Subject, route.SelectedSubject))
                .ToList();
            if (scopedChunks.Count == 0)
            {
                return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), route.SelectedSubject);
            }
        }

        var queryTerms = ExtractTerms(resolvedQuestion);

        var contentTerms = RemoveCourseScopeTerms(queryTerms);
        var needsContentEvidence = contentTerms.Count > 0;
        var minimumSharedTerms = contentTerms.Count >= 4 ? 2 : 1;
        var queryEmbedding = await _embeddingService.EmbedAsync(resolvedQuestion, cancellationToken);

        var candidateMatches = scopedChunks
            .Select(chunk => new
            {
                Chunk = chunk,
                VectorScore = _embeddingService.CosineSimilarity(queryEmbedding, chunk.Embedding),
                TextSharedTerms = CountSharedTerms(queryTerms, chunk.Text),
                ContentSharedTerms = CountSharedTerms(contentTerms, chunk.Text),
                MetadataSharedTerms = CountSharedTerms(queryTerms, BuildChunkMetadataText(chunk))
            })
            .Select(item => new ScoredChunk(
                item.Chunk,
                CalculateRetrievalScore(
                    item.VectorScore,
                    item.TextSharedTerms,
                    item.MetadataSharedTerms,
                    item.ContentSharedTerms,
                    queryTerms.Count,
                    contentTerms.Count),
                item.TextSharedTerms,
                item.ContentSharedTerms,
                item.TextSharedTerms + item.MetadataSharedTerms,
                item.MetadataSharedTerms))
            .Where(item => item.Score >= MinimumScore
                           && item.TextSharedTerms >= minimumSharedTerms
                           && (!needsContentEvidence || item.ContentSharedTerms > 0))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ContentSharedTerms)
            .ThenByDescending(item => item.TextSharedTerms)
            .ThenByDescending(item => item.MetadataSharedTerms)
            .Take(RerankCandidateK)
            .ToList();

        if (candidateMatches.Count == 0)
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>());
        }

        if (string.IsNullOrWhiteSpace(route.SelectedSubject))
        {
            var ambiguousSubjects = FindAmbiguousCandidateSubjects(candidateMatches);
            if (ambiguousSubjects.Count > 1)
            {
                return new SingleQuestionAnswer(
                    question,
                    BuildSubjectClarificationAnswer(ambiguousSubjects, responseLanguage),
                    Array.Empty<SourceCitation>(),
                    null,
                    true,
                    ambiguousSubjects);
            }
        }

        var matches = await RerankMatchesAsync(resolvedQuestion, candidateMatches, responseLanguage, cancellationToken);

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
        var resolvedSubject = route.SelectedSubject ?? ResolveSubject(matchedChunks);
        var answer = await _chatCompletionService.GenerateAnswerAsync(
                         resolvedQuestion,
                         resolvedSubject,
                         historyBeforeQuestion,
                         matchedChunks,
                         responseLanguage,
                         cancellationToken)
                     ?? BuildGroundedAnswer(queryTerms, matchedChunks, responseLanguage);

        if (IsInsufficientDataAnswer(answer))
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>());
        }

        var contextText = string.Join("\n\n", matchedChunks.Select(chunk => chunk.Text));
        if (!IsAnswerGrounded(answer, contextText, queryTerms))
        {
            answer = BuildGroundedAnswer(queryTerms, matchedChunks, responseLanguage);
            if (IsInsufficientDataAnswer(answer))
            {
                return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>());
            }
        }

        return new SingleQuestionAnswer(question, answer, citations, resolvedSubject);
    }

    private async Task<IReadOnlyList<DocumentChunk>> GetScopedChunksAsync(
        IReadOnlyCollection<string>? allowedSubjects,
        CancellationToken cancellationToken)
    {
        var chunks = await _repository.GetChunksAsync(cancellationToken);
        var normalizedAllowedSubjects = allowedSubjects?
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedAllowedSubjects is null || normalizedAllowedSubjects.Count == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        return chunks
            .Where(chunk => normalizedAllowedSubjects.Any(subject => SubjectMatches(chunk.Subject, subject)))
            .ToList();
    }

    private static SubjectRouteResult ResolveSubjectRoute(
        string question,
        string? subjectFilter,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var subjects = chunks
            .Select(chunk => chunk.Subject)
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(subject => subject)
            .ToList();

        if (subjects.Count == 0)
        {
            return new SubjectRouteResult(null, false, Array.Empty<string>());
        }

        if (!string.IsNullOrWhiteSpace(subjectFilter) && !IsAllSubjectsFilter(subjectFilter))
        {
            var selected = subjects.FirstOrDefault(subject => SubjectMatches(subject, subjectFilter))
                ?? subjectFilter.Trim();
            return new SubjectRouteResult(selected, false, Array.Empty<string>());
        }

        if (subjects.Count == 1)
        {
            return new SubjectRouteResult(subjects[0], false, Array.Empty<string>());
        }

        var normalizedQuestion = NormalizeQuestion(question);
        var explicitMatches = subjects
            .Where(subject => GetSubjectAliases(subject).Any(alias => normalizedQuestion.Contains(alias, StringComparison.Ordinal)))
            .ToList();

        if (explicitMatches.Count == 1)
        {
            return new SubjectRouteResult(explicitMatches[0], false, Array.Empty<string>());
        }

        if (explicitMatches.Count > 1 && !IsMultiSubjectQuestion(normalizedQuestion))
        {
            return new SubjectRouteResult(null, true, explicitMatches.Take(5).ToList());
        }

        return new SubjectRouteResult(null, false, Array.Empty<string>());
    }

    private static IReadOnlyList<string> FindAmbiguousCandidateSubjects(IReadOnlyList<ScoredChunk> candidates)
    {
        var ranked = candidates
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Chunk.Subject) ? "Không rõ môn" : item.Chunk.Subject.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Subject = group.Key,
                Score = group.Max(item => item.Score) + Math.Min(0.03, group.Count() * 0.005)
            })
            .OrderByDescending(item => item.Score)
            .Take(5)
            .ToList();

        if (ranked.Count < 2)
        {
            return Array.Empty<string>();
        }

        var top = ranked[0];
        var second = ranked[1];
        return top.Score - second.Score <= 0.08
            ? ranked.Select(item => item.Subject).ToList()
            : Array.Empty<string>();
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

    private static bool IsAllSubjectsFilter(string value)
    {
        var normalized = NormalizeQuestion(value);
        return normalized is "all" or "all subjects" or "tat ca" or "tat ca mon";
    }

    private static IReadOnlyList<string> GetSubjectAliases(string subject)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedSubject = NormalizeQuestion(subject);
        if (normalizedSubject.Length >= 3)
        {
            aliases.Add(normalizedSubject);
        }

        var code = ExtractSubjectCode(subject);
        var normalizedCode = NormalizeQuestion(code);
        if (normalizedCode.Length >= 3)
        {
            aliases.Add(normalizedCode);
            aliases.Add(normalizedCode.Replace(" ", string.Empty, StringComparison.Ordinal));
        }

        var separatorIndex = subject.IndexOf('-', StringComparison.Ordinal);
        if (separatorIndex >= 0 && separatorIndex + 1 < subject.Length)
        {
            var name = NormalizeQuestion(subject[(separatorIndex + 1)..]);
            if (name.Length >= 6)
            {
                aliases.Add(name);
            }
        }

        return aliases.ToList();
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

    private static bool IsMultiSubjectQuestion(string normalizedQuestion)
    {
        return normalizedQuestion.Contains("so sanh", StringComparison.Ordinal)
               || normalizedQuestion.Contains("compare", StringComparison.Ordinal)
               || normalizedQuestion.Contains("tat ca mon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("cac mon", StringComparison.Ordinal)
               || normalizedQuestion.Contains("all subjects", StringComparison.Ordinal);
    }

    private static string BuildSubjectClarificationAnswer(IReadOnlyList<string> subjects, string language)
    {
        var options = subjects
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        if (options.Count == 0)
        {
            return BuildOutOfScopeAnswer(language);
        }

        var optionText = string.Join("\n", options.Select(subject => $"- {subject}"));
        return language == "vi"
            ? $"Mình tìm thấy dữ liệu liên quan ở nhiều môn. Bạn muốn hỏi môn nào?\n\n{optionText}"
            : $"I found relevant data in multiple subjects. Which subject do you want?\n\n{optionText}";
    }

    private async Task<IReadOnlyList<ScoredChunk>> RerankMatchesAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        string language,
        CancellationToken cancellationToken)
    {
        var fallback = candidates.Take(TopK).ToList();
        if (candidates.Count == 0)
        {
            return fallback;
        }

        IReadOnlyList<ChatChunkRerankResult> reranked;
        try
        {
            reranked = await _chatCompletionService.RerankChunksAsync(
                question,
                candidates.Select(item => item.Chunk).ToList(),
                language,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return fallback;
        }

        if (reranked.Count == 0)
        {
            return fallback;
        }

        var selected = new List<ScoredChunk>();
        var seen = new HashSet<int>();
        foreach (var decision in reranked.OrderByDescending(item => item.Score))
        {
            var candidateIndex = decision.CandidateNumber - 1;
            if (candidateIndex < 0 || candidateIndex >= candidates.Count || !seen.Add(candidateIndex))
            {
                continue;
            }

            var candidate = candidates[candidateIndex];
            var geminiConfidence = Math.Clamp(decision.Score, 0, 1);
            var boostedScore = Math.Round(Math.Max(candidate.Score, 0.82 + (geminiConfidence * 0.18)), 3);
            selected.Add(candidate with { Score = boostedScore });
            if (selected.Count == TopK)
            {
                break;
            }
        }

        return selected.Count == 0 ? fallback : selected;
    }

    private static IReadOnlyList<string> SplitQuestionBatch(string input)
    {
        var lines = input
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanQuestionLine)
            .Where(IsLikelyQuestion)
            .ToList();

        if (lines.Count > 1)
        {
            return lines;
        }

        var inlineQuestions = Regex.Matches(input, @"[^?？！]+[?？！]")
            .Select(match => CleanQuestionLine(match.Value))
            .Where(IsLikelyQuestion)
            .ToList();

        return inlineQuestions.Count > 1 ? inlineQuestions : Array.Empty<string>();
    }

    private static string CleanQuestionLine(string line)
    {
        var cleaned = Regex.Replace(line.Trim(), @"^\s*(?:[-*•]|\d+[\).\:-])\s*", string.Empty);
        var pipeIndex = cleaned.IndexOf('|', StringComparison.Ordinal);
        if (pipeIndex > 0)
        {
            cleaned = cleaned[..pipeIndex].Trim();
        }

        return cleaned.Trim();
    }

    private static bool IsLikelyQuestion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.EndsWith('?') || value.EndsWith('？') || value.EndsWith('!'))
        {
            return true;
        }

        var normalized = NormalizeQuestion(value);
        return normalized.Contains(" la gi", StringComparison.Ordinal)
               || normalized.Contains("bao nhieu", StringComparison.Ordinal)
               || normalized.Contains("nhu the nao", StringComparison.Ordinal)
               || normalized.Contains("noi dung nao", StringComparison.Ordinal)
               || normalized.Contains("yeu cau", StringComparison.Ordinal)
               || normalized.Contains("what", StringComparison.Ordinal)
               || normalized.Contains("how", StringComparison.Ordinal)
               || normalized.Contains("which", StringComparison.Ordinal);
    }

    private static string FormatBatchAnswer(
        IReadOnlyList<SingleQuestionAnswer> answers,
        int skippedQuestionCount,
        string language)
    {
        var builder = new StringBuilder();
        builder.AppendLine(language == "vi"
            ? $"Mình nhận {answers.Count} câu hỏi. Trả lời lần lượt:"
            : $"I received {answers.Count} questions. Here are the answers in order:");
        builder.AppendLine();

        for (var index = 0; index < answers.Count; index++)
        {
            var item = answers[index];
            builder.AppendLine($"{index + 1}. {item.Question}");
            builder.AppendLine(item.Answer.Trim());
            builder.AppendLine();
        }

        if (skippedQuestionCount > 0)
        {
            builder.AppendLine(language == "vi"
                ? $"Mình chỉ xử lý tối đa {MaxBatchQuestions} câu mỗi lần, còn {skippedQuestionCount} câu chưa xử lý. Hãy gửi tiếp phần còn lại ở tin nhắn sau."
                : $"I only process up to {MaxBatchQuestions} questions per message. {skippedQuestionCount} questions were not processed; send them in the next message.");
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<SourceCitation> MergeCitations(IEnumerable<SourceCitation> citations)
    {
        return citations
            .GroupBy(item => new { item.DocumentId, item.ChunkIndex })
            .Select(group => group.First())
            .OrderByDescending(item => item.Score)
            .Take(20)
            .ToList();
    }

    private sealed record SingleQuestionAnswer(
        string Question,
        string Answer,
        IReadOnlyList<SourceCitation> Citations,
        string? ResolvedSubject = null,
        bool NeedsClarification = false,
        IReadOnlyList<string>? SubjectOptions = null);

    private sealed record SubjectRouteResult(
        string? SelectedSubject,
        bool NeedsClarification,
        IReadOnlyList<string> CandidateSubjects);

    private sealed record ScoredChunk(
        DocumentChunk Chunk,
        double Score,
        int TextSharedTerms,
        int ContentSharedTerms,
        int SharedTerms,
        int MetadataSharedTerms);

    private async Task<ChatAnswer> SaveAssistantAnswer(
        Guid sessionId,
        string answer,
        IReadOnlyList<SourceCitation> citations,
        CancellationToken cancellationToken,
        ChatSessionOwnerInfo? ownerInfo = null,
        string? resolvedSubject = null,
        bool needsClarification = false,
        IReadOnlyList<string>? subjectOptions = null)
    {
        await _repository.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "assistant",
            Content = answer,
            Citations = citations.ToList()
        }, cancellationToken, ownerInfo);

        var session = await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken, ownerInfo);
        return new ChatAnswer(answer, citations, session.Messages, resolvedSubject, needsClarification, subjectOptions ?? Array.Empty<string>());
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
        var compactTokens = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var terms = ExtractTerms(question);

        return CasualChatSignals.Contains(compact)
               || (terms.Count <= 2 && CasualChatSignals.Any(signal => compactTokens.Contains(signal, StringComparer.Ordinal)));
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
        return $"Bạn là {name}. Trong ứng dụng này, bạn là chủ kho tài liệu và là người mình đang hỗ trợ.";
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

    private static bool IsAnswerGrounded(
        string answer,
        string contextText,
        IReadOnlySet<string> questionTerms)
    {
        if (string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(contextText))
        {
            return false;
        }

        var answerFacts = ExtractGroundingFacts(answer);
        if (answerFacts.Count > 0)
        {
            var contextFacts = ExtractGroundingFacts(contextText);
            if (answerFacts.Any(fact => !contextFacts.Contains(fact)))
            {
                return false;
            }
        }

        var answerTerms = ExtractTerms(answer);
        answerTerms.ExceptWith(questionTerms);
        answerTerms.RemoveWhere(term => AnswerScaffoldTerms.Contains(term));
        if (answerTerms.Count == 0)
        {
            return true;
        }

        var contextTerms = ExtractTerms(contextText);
        if (contextTerms.Count == 0)
        {
            return false;
        }

        var groundedTerms = answerTerms.Count(answerTerm => contextTerms.Any(contextTerm => TermsMatch(answerTerm, contextTerm)));
        var groundingRatio = groundedTerms / (double)answerTerms.Count;
        var requiredRatio = answerTerms.Count <= 4 ? 0.5 : MinimumAnswerGroundingRatio;
        return groundingRatio >= requiredRatio;
    }

    private static HashSet<string> ExtractGroundingFacts(string text)
    {
        return Regex.Matches(NormalizeQuestion(text), @"\b(?:[a-z]{2,}\d{2,}|\d+(?:[.,]\d+)?%?)\b")
            .Select(match => match.Value.Replace(',', '.'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        int contentSharedTerms,
        int queryTermCount,
        int contentTermCount)
    {
        if (queryTermCount == 0)
        {
            return vectorScore;
        }

        if (contentTermCount > 0 && contentSharedTerms == 0)
        {
            return vectorScore;
        }

        var enoughLexicalEvidence = contentTermCount == 0
            ? textSharedTerms > 0
            : contentSharedTerms >= (contentTermCount >= 4 ? 2 : 1);

        if (!enoughLexicalEvidence)
        {
            return vectorScore;
        }

        var textCoverage = textSharedTerms / (double)queryTermCount;
        var contentCoverage = contentTermCount == 0 ? textCoverage : contentSharedTerms / (double)contentTermCount;
        var metadataBoost = metadataSharedTerms > 0 ? 0.08 : 0;
        var lexicalScore = (contentCoverage * 0.62) + (textCoverage * 0.30) + metadataBoost;
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

    private static HashSet<string> RemoveCourseScopeTerms(IReadOnlySet<string> terms)
    {
        var scopedTerms = new HashSet<string>(terms, StringComparer.OrdinalIgnoreCase);
        scopedTerms.RemoveWhere(term =>
        {
            var normalized = Regex.Replace(term ?? string.Empty, @"[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase);
            return Regex.IsMatch(normalized, @"^[a-z]{2,}\d{2,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                   || normalized.Equals("syllabus", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("subject", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("course", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("mon", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("hoc", StringComparison.OrdinalIgnoreCase);
        });
        return scopedTerms;
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
