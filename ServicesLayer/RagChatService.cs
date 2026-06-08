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
    IReadOnlyList<string>? SubjectOptions = null,
    string AnswerSource = "Rag",
    bool HasDirectCitation = true,
    string? FallbackModel = null);

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
    private const int RerankCandidateK = 20;
    private const int MaxBatchQuestions = 50;
    private const double MinimumScore = 0.42;
    private const double MinimumLocalRerankScore = 0.48;
    private const double MinimumGeminiRerankScore = 0.55;
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
        "bye",
        "an com chua",
        "com chua",
        "khoe khong",
        "on khong"
    };

    private static readonly string[] ExternalQuestionSignals =
    {
        "thoi tiet",
        "weather",
        "nhiet do",
        "temperature",
        "du bao",
        "forecast",
        "hom nay mua khong",
        "may gio",
        "what time",
        "current time",
        "tin tuc",
        "news today",
        "gia vang",
        "stock price",
        "ty gia",
        "exchange rate",
        "ket qua bong da",
        "lich thi dau"
    };

    private static readonly string[] DocumentQuestionSignals =
    {
        "tai lieu",
        "document",
        "syllabus",
        "mon",
        "hoc",
        "course",
        "subject",
        "chapter",
        "chuong",
        "tin chi",
        "credit",
        "nocredit",
        "assessment",
        "danh gia",
        "quiz",
        "exam",
        "assignment",
        "clo",
        "learning outcome",
        "requirement",
        "yeu cau",
        "noi dung",
        "bai hoc",
        "lecture"
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
        "answer", "based", "below", "checked", "citation", "cited", "course", "data", "document", "documents", "found", "from",
        "information", "ok", "okay", "quick", "sure", "student", "source", "sources",
        "ban", "cau", "co", "day", "du", "duoi", "gon", "hoi", "lieu", "minh", "nha", "nhe", "nguon", "noi", "phan", "roi",
        "sau", "sinh", "tai", "theo", "thay", "thong", "tin", "toi", "tom", "tra", "trich", "vien", "xem"
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
            singleAnswer.SubjectOptions,
            singleAnswer.AnswerSource,
            singleAnswer.HasDirectCitation,
            singleAnswer.FallbackModel);
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
            return new SingleQuestionAnswer(question, BuildBotIdentityAnswer(responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "SmallTalk", HasDirectCitation: false);
        }

        if (IsUserIdentityQuestion(question))
        {
            return new SingleQuestionAnswer(question, BuildUserIdentityAnswer(userDisplayName, responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "SmallTalk", HasDirectCitation: false);
        }

        var intent = await ClassifyQuestionIntentAsync(
            question,
            historyBeforeQuestion,
            responseLanguage,
            cancellationToken);

        if (intent.Intent == ChatQueryIntent.Unsafe)
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "OutOfScope", HasDirectCitation: false);
        }

        if (intent.Intent == ChatQueryIntent.SmallTalk)
        {
            return new SingleQuestionAnswer(question, BuildCasualChatAnswer(question, responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "SmallTalk", HasDirectCitation: false);
        }

        if (intent.Intent == ChatQueryIntent.ExternalQuestion)
        {
            return new SingleQuestionAnswer(question, BuildExternalScopeAnswer(responseLanguage), Array.Empty<SourceCitation>(), AnswerSource: "OutOfScope", HasDirectCitation: false);
        }

        var retrievalQueries = await BuildRetrievalQueriesAsync(
            question,
            historyBeforeQuestion,
            cancellationToken);
        var resolvedQuestion = retrievalQueries.FirstOrDefault(query => !query.Equals(question, StringComparison.OrdinalIgnoreCase))
                               ?? question;
        var retrievalQuestion = string.Join("\n", retrievalQueries);

        scopedChunks ??= await GetScopedChunksAsync(allowedSubjects, cancellationToken);
        if (scopedChunks.Count == 0)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                subjectFilter,
                historyBeforeQuestion,
                Array.Empty<DocumentChunk>(),
                allowedSubjects,
                responseLanguage,
                cancellationToken);
        }

        var route = ResolveSubjectRoute(retrievalQuestion, subjectFilter, scopedChunks);
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
                return await BuildInsufficientAnswerAsync(
                    question,
                    route.SelectedSubject,
                    historyBeforeQuestion,
                    Array.Empty<DocumentChunk>(),
                    allowedSubjects,
                    responseLanguage,
                    cancellationToken);
            }
        }

        if (TryBuildExactFactAnswer(retrievalQuestion, scopedChunks, responseLanguage) is { } exactFactAnswer)
        {
            return exactFactAnswer with { ResolvedSubject = route.SelectedSubject ?? exactFactAnswer.ResolvedSubject };
        }

        if (TryBuildSubjectOverviewAnswer(retrievalQuestion, scopedChunks, responseLanguage) is { } subjectOverviewAnswer)
        {
            return subjectOverviewAnswer with { ResolvedSubject = route.SelectedSubject ?? subjectOverviewAnswer.ResolvedSubject };
        }

        var queryTerms = ExtractTerms(retrievalQuestion);

        var contentTerms = RemoveCourseScopeTerms(queryTerms);
        var candidateMatches = await BuildCandidateMatchesAsync(
            retrievalQueries,
            queryTerms,
            contentTerms,
            scopedChunks,
            cancellationToken);

        if (candidateMatches.Count == 0)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                route.SelectedSubject ?? subjectFilter,
                historyBeforeQuestion,
                scopedChunks,
                allowedSubjects,
                responseLanguage,
                cancellationToken);
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

        var matches = await RerankMatchesAsync(retrievalQuestion, candidateMatches, responseLanguage, cancellationToken);
        if (matches.Count == 0)
        {
            return await BuildInsufficientAnswerAsync(
                question,
                route.SelectedSubject ?? subjectFilter,
                historyBeforeQuestion,
                scopedChunks,
                allowedSubjects,
                responseLanguage,
                cancellationToken);
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
        var resolvedSubject = route.SelectedSubject ?? ResolveSubject(matchedChunks);
        var generatedAnswer = await _chatCompletionService.GenerateAnswerAsync(
            resolvedQuestion,
            resolvedSubject,
            historyBeforeQuestion,
            matchedChunks,
            responseLanguage,
            cancellationToken);
        var answer = generatedAnswer ?? BuildGroundedAnswer(queryTerms, matchedChunks, responseLanguage);

        if (IsInsufficientDataAnswer(answer))
        {
            return await BuildInsufficientAnswerAsync(
                question,
                resolvedSubject,
                historyBeforeQuestion,
                scopedChunks,
                allowedSubjects,
                responseLanguage,
                cancellationToken);
        }

        if (!await IsAnswerGroundedAsync(resolvedQuestion, answer, matchedChunks, queryTerms, responseLanguage, cancellationToken))
        {
            return await BuildInsufficientAnswerAsync(
                question,
                resolvedSubject,
                historyBeforeQuestion,
                scopedChunks,
                allowedSubjects,
                responseLanguage,
                cancellationToken);
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

    private async Task<SingleQuestionAnswer> BuildInsufficientAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        IReadOnlyList<DocumentChunk> subjectChunks,
        IReadOnlyCollection<string>? allowedSubjects,
        string responseLanguage,
        CancellationToken cancellationToken)
    {
        var documentFallback = await TryBuildDocumentFallbackAnswerAsync(
            question,
            subject,
            historyBeforeQuestion,
            subjectChunks,
            responseLanguage,
            cancellationToken);
        if (documentFallback is not null)
        {
            return documentFallback;
        }

        return new SingleQuestionAnswer(
            question,
            BuildOutOfScopeAnswer(responseLanguage),
            Array.Empty<SourceCitation>(),
            subject,
            AnswerSource: "OutOfScope",
            HasDirectCitation: false);
    }

    private async Task<SingleQuestionAnswer?> TryBuildDocumentFallbackAnswerAsync(
        string question,
        string? subject,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        IReadOnlyList<DocumentChunk> subjectChunks,
        string responseLanguage,
        CancellationToken cancellationToken)
    {
        if (subjectChunks.Count == 0)
        {
            return null;
        }

        if (TryBuildIndexedPartsAnswer(question, subjectChunks, responseLanguage) is { } indexedPartsAnswer)
        {
            return indexedPartsAnswer;
        }

        var fallbackChunks = SelectDocumentFallbackChunks(question, subjectChunks);
        if (fallbackChunks.Count == 0)
        {
            return null;
        }

        var resolvedSubject = string.IsNullOrWhiteSpace(subject) ? ResolveSubject(fallbackChunks) : subject.Trim();
        var queryTerms = ExtractTerms(question);
        var answerSelectionTerms = new HashSet<string>(queryTerms, StringComparer.OrdinalIgnoreCase);
        answerSelectionTerms.UnionWith(BuildDocumentFallbackTerms(question));
        string? generatedAnswer = null;
        try
        {
            generatedAnswer = await _chatCompletionService.GenerateAnswerAsync(
                question,
                resolvedSubject,
                historyBeforeQuestion,
                fallbackChunks,
                responseLanguage,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            generatedAnswer = null;
        }

        var answer = string.IsNullOrWhiteSpace(generatedAnswer) || IsInsufficientDataAnswer(generatedAnswer)
            ? BuildGroundedAnswer(answerSelectionTerms, fallbackChunks, responseLanguage)
            : generatedAnswer.Trim();
        if (IsInsufficientDataAnswer(answer))
        {
            return null;
        }

        if (!await IsAnswerGroundedAsync(question, answer, fallbackChunks, queryTerms, responseLanguage, cancellationToken))
        {
            return null;
        }

        return new SingleQuestionAnswer(
            question,
            answer,
            BuildCitations(fallbackChunks, 0.74),
            resolvedSubject,
            AnswerSource: "Rag",
            HasDirectCitation: true);
    }

    private static SingleQuestionAnswer? TryBuildIndexedPartsAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        if (!IsIndexedPartsQuestion(question) || chunks.Count == 0)
        {
            return null;
        }

        var grouped = chunks
            .GroupBy(chunk => string.IsNullOrWhiteSpace(chunk.Chapter) ? "Kh\u00f4ng r\u00f5 ch\u01b0\u01a1ng" : chunk.Chapter.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(chunk => chunk.ChunkIndex))
            .Take(12)
            .ToList();
        if (grouped.Count == 0)
        {
            return null;
        }

        var subject = ResolveSubject(chunks);
        var lines = grouped.Select(group =>
        {
            var sectionTitles = group
                .Select(chunk => chunk.SectionTitle)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();
            var suffix = sectionTitles.Count == 0 ? string.Empty : $" ({string.Join(", ", sectionTitles)})";
            return language == "vi"
                ? $"- {group.Key}{suffix}: {group.Count()} chunk"
                : $"- {group.Key}{suffix}: {group.Count()} chunks";
        });

        var answer = language == "vi"
            ? $"M\u00ecnh th\u1ea5y {ExtractSubjectCode(subject)} \u0111\u00e3 index c\u00e1c ch\u01b0\u01a1ng/ph\u1ea7n sau:\n\n{string.Join("\n", lines)}"
            : $"I found these indexed chapters/sections for {ExtractSubjectCode(subject)}:\n\n{string.Join("\n", lines)}";

        var citationChunks = grouped.Select(group => group.First()).Take(5).ToList();
        return new SingleQuestionAnswer(
            question,
            answer,
            BuildCitations(citationChunks, 0.9),
            subject,
            AnswerSource: "Rag",
            HasDirectCitation: true);
    }

    private static bool IsIndexedPartsQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return (normalized.Contains("index", StringComparison.Ordinal)
                || normalized.Contains("da index", StringComparison.Ordinal)
                || normalized.Contains("indexed", StringComparison.Ordinal))
               && (normalized.Contains("chuong", StringComparison.Ordinal)
                   || normalized.Contains("phan", StringComparison.Ordinal)
                   || normalized.Contains("section", StringComparison.Ordinal)
                   || normalized.Contains("chapter", StringComparison.Ordinal));
    }

    private static IReadOnlyList<DocumentChunk> SelectDocumentFallbackChunks(
        string question,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var questionTerms = ExtractTerms(question);
        var intentTerms = BuildDocumentFallbackTerms(question);
        var terms = new HashSet<string>(questionTerms, StringComparer.OrdinalIgnoreCase);
        terms.UnionWith(intentTerms);
        if (terms.Count == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        return chunks
            .Select(chunk =>
            {
                var textScore = CountSharedTerms(terms, chunk.Text);
                var queryTextScore = CountSharedTerms(questionTerms, chunk.Text);
                var intentTextScore = CountSharedTerms(intentTerms, chunk.Text);
                var metadataScore = CountSharedTerms(terms, BuildChunkMetadataText(chunk));
                var score = (intentTextScore * 2.0) + queryTextScore + (textScore * 0.3) + (metadataScore * 0.4);
                return new { Chunk = chunk, Score = score, IntentTextScore = intentTextScore };
            })
            .Where(item => item.Score >= 1.0 && (intentTerms.Count == 0 || item.IntentTextScore > 0))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.ChunkIndex)
            .Select(item => item.Chunk)
            .Take(6)
            .ToList();
    }

    private static HashSet<string> BuildDocumentFallbackTerms(string question)
    {
        var normalized = NormalizeQuestion(question);
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalized.Contains("danh gia", StringComparison.Ordinal)
            || normalized.Contains("ty le", StringComparison.Ordinal)
            || normalized.Contains("thi", StringComparison.Ordinal)
            || normalized.Contains("assessment", StringComparison.Ordinal)
            || normalized.Contains("exam", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "assessment", "exam", "final", "quiz", "participation", "assignment", "weight", "percentage", "criteria", "grading",
                "danh", "gia", "diem", "thi", "cuoi", "ky", "thuc", "hanh", "tham", "gia", "ty", "le"
            });
        }

        if (normalized.Contains("tai lieu", StringComparison.Ordinal)
            || normalized.Contains("nguon", StringComparison.Ordinal)
            || normalized.Contains("resource", StringComparison.Ordinal)
            || normalized.Contains("material", StringComparison.Ordinal)
            || normalized.Contains("textbook", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "resource", "resources", "material", "materials", "textbook", "book", "url", "website", "youtube", "clip",
                "tai", "lieu", "nguon", "hoc", "giao", "trinh", "tham", "khao"
            });
        }

        if (normalized.Contains("sinh vien", StringComparison.Ordinal)
            || normalized.Contains("can lam", StringComparison.Ordinal)
            || normalized.Contains("lam gi", StringComparison.Ordinal)
            || normalized.Contains("student", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "student", "students", "learner", "learners", "activity", "activities", "practice", "assignment", "completion", "criteria",
                "sinh", "vien", "lam", "thuc", "hanh", "bai", "tap", "hoat", "dong", "yeu", "cau"
            });
        }

        if (normalized.Contains("chuan dau ra", StringComparison.Ordinal)
            || normalized.Contains("outcome", StringComparison.Ordinal)
            || normalized.Contains("clo", StringComparison.Ordinal))
        {
            terms.UnionWith(new[]
            {
                "clo", "outcome", "outcomes", "learning", "objective", "objectives", "knowledge", "skill",
                "chuan", "dau", "ra", "muc", "tieu", "kien", "thuc", "ky", "nang"
            });
        }

        return terms;
    }

    private static IReadOnlyList<SourceCitation> BuildCitations(IReadOnlyList<DocumentChunk> chunks, double score)
    {
        return chunks
            .GroupBy(chunk => new { chunk.DocumentId, chunk.ChunkIndex })
            .Select(group => group.First())
            .Take(5)
            .Select(chunk => new SourceCitation
            {
                DocumentId = chunk.DocumentId,
                FileName = chunk.FileName,
                Subject = chunk.Subject,
                Chapter = chunk.Chapter,
                ChunkIndex = chunk.ChunkIndex,
                Score = score,
                Excerpt = CreateExcerpt(chunk.Text)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ScoredChunk>> BuildCandidateMatchesAsync(
        IReadOnlyList<string> retrievalQueries,
        IReadOnlySet<string> queryTerms,
        IReadOnlySet<string> contentTerms,
        IReadOnlyList<DocumentChunk> scopedChunks,
        CancellationToken cancellationToken)
    {
        var needsContentEvidence = contentTerms.Count > 0;
        var minimumSharedTerms = contentTerms.Count >= 4 ? 2 : 1;
        var queryEmbeddings = new List<Dictionary<int, double>>();

        foreach (var query in retrievalQueries.Where(query => !string.IsNullOrWhiteSpace(query)).Take(4))
        {
            queryEmbeddings.Add(await _embeddingService.EmbedAsync(query, cancellationToken));
        }

        if (queryEmbeddings.Count == 0)
        {
            queryEmbeddings.Add(await _embeddingService.EmbedAsync(string.Join(" ", queryTerms), cancellationToken));
        }

        var courseCodes = ExtractCourseCodes(string.Join("\n", retrievalQueries));

        return scopedChunks
            .Select(chunk =>
            {
                var vectorScore = queryEmbeddings.Count == 0
                    ? 0
                    : queryEmbeddings.Max(embedding => _embeddingService.CosineSimilarity(embedding, chunk.Embedding));
                var textSharedTerms = CountSharedTerms(queryTerms, chunk.Text);
                var contentSharedTerms = CountSharedTerms(contentTerms, chunk.Text);
                var metadataSharedTerms = CountSharedTerms(queryTerms, BuildChunkMetadataText(chunk));
                var lexicalScore = CalculateLexicalScore(
                    textSharedTerms,
                    metadataSharedTerms,
                    contentSharedTerms,
                    queryTerms.Count,
                    contentTerms.Count);
                var courseCodeMatched = courseCodes.Count > 0
                                        && CountSharedTerms(courseCodes, BuildChunkMetadataText(chunk)) > 0;

                return new ScoredChunk(
                    chunk,
                    CalculateRetrievalScore(vectorScore, lexicalScore, courseCodeMatched),
                    textSharedTerms,
                    contentSharedTerms,
                    textSharedTerms + metadataSharedTerms,
                    metadataSharedTerms,
                    Math.Clamp(vectorScore, 0, 1),
                    lexicalScore);
            })
            .Where(item => HasRetrievalEvidence(item, needsContentEvidence, minimumSharedTerms))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ContentSharedTerms)
            .ThenByDescending(item => item.TextSharedTerms)
            .ThenByDescending(item => item.MetadataSharedTerms)
            .ThenByDescending(item => item.VectorScore)
            .Take(RerankCandidateK)
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

        if (!string.IsNullOrWhiteSpace(subjectFilter) && !IsAllSubjectsFilter(subjectFilter))
        {
            var selected = subjects.FirstOrDefault(subject => SubjectMatches(subject, subjectFilter))
                ?? subjectFilter.Trim();
            return new SubjectRouteResult(selected, false, Array.Empty<string>());
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

            var codePrefix = Regex.Match(normalizedCode, @"^[a-z]+", RegexOptions.CultureInvariant).Value;
            if (codePrefix.Length >= 3)
            {
                aliases.Add(codePrefix);
            }
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
            ? $"M\u00ecnh th\u1ea5y c\u00e2u n\u00e0y ch\u1ea1m t\u1edbi v\u00e0i m\u00f4n. B\u1ea1n ch\u1ecdn \u0111\u00fang m\u00f4n gi\u00fap m\u00ecnh nh\u00e9:\n\n{optionText}"
            : $"I found relevant data in a few subjects. Which one should I use?\n\n{optionText}";
    }

    private static SingleQuestionAnswer? TryBuildExactFactAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        var factIntent = DetectExactFactIntent(question);
        if (factIntent != ExactFactIntent.Credits)
        {
            return null;
        }

        var evidence = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Credit = TryExtractCreditFact(chunk.Text),
                SubjectCodeScore = CountSharedTerms(ExtractCourseCodes(question), BuildChunkMetadataText(chunk))
            })
            .Where(item => item.Credit is not null)
            .OrderByDescending(item => item.SubjectCodeScore)
            .ThenBy(item => item.Chunk.ChunkIndex)
            .FirstOrDefault();

        if (evidence?.Credit is null)
        {
            return null;
        }

        var courseLabel = ResolveFactCourseLabel(question, evidence.Chunk);
        var answer = language == "vi"
            ? $"M\u00ecnh xem trong t\u00e0i li\u1ec7u r\u1ed3i: {courseLabel} c\u00f3 {FormatCreditValue(evidence.Credit.Value)} t\u00edn ch\u1ec9."
            : $"I checked the documents: {courseLabel} has {FormatCreditValue(evidence.Credit.Value)} credits.";
        var citations = new[]
        {
            new SourceCitation
            {
                DocumentId = evidence.Chunk.DocumentId,
                FileName = evidence.Chunk.FileName,
                Subject = evidence.Chunk.Subject,
                Chapter = evidence.Chunk.Chapter,
                ChunkIndex = evidence.Chunk.ChunkIndex,
                Score = 0.99,
                Excerpt = CreateFactExcerpt(evidence.Chunk.Text, evidence.Credit.EvidenceText)
            }
        };

        return new SingleQuestionAnswer(question, answer, citations, ResolveSubject(new[] { evidence.Chunk }));
    }

    private static SingleQuestionAnswer? TryBuildSubjectOverviewAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language)
    {
        if (!IsSubjectOverviewQuestion(question) || chunks.Count == 0)
        {
            return null;
        }

        var evidence = chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .FirstOrDefault(chunk => !string.IsNullOrWhiteSpace(chunk.Subject) || !string.IsNullOrWhiteSpace(chunk.Text));
        if (evidence is null)
        {
            return null;
        }

        var subject = string.IsNullOrWhiteSpace(evidence.Subject) ? ResolveSubject(chunks) : evidence.Subject.Trim();
        var subjectCode = ExtractSubjectCode(subject);
        var subjectName = ResolveSubjectName(subject, evidence.Text);
        var credit = TryExtractCreditFact(evidence.Text);
        var displayCode = string.IsNullOrWhiteSpace(subjectCode) ? subject : subjectCode;
        var displayName = string.IsNullOrWhiteSpace(subjectName) ? subject : subjectName;

        var answer = language == "vi"
            ? $"M\u00ecnh th\u1ea5y trong syllabus: {displayCode} l\u00e0 m\u00f4n {displayName}."
            : $"I found this in the syllabus: {displayCode} is the course {displayName}.";
        if (credit is not null)
        {
            answer += language == "vi"
                ? $" Môn này có {FormatCreditValue(credit.Value)} tín chỉ."
                : $" This course has {FormatCreditValue(credit.Value)} credits.";
        }

        var citations = new[]
        {
            new SourceCitation
            {
                DocumentId = evidence.DocumentId,
                FileName = evidence.FileName,
                Subject = evidence.Subject,
                Chapter = evidence.Chapter,
                ChunkIndex = evidence.ChunkIndex,
                Score = 0.96,
                Excerpt = CreateExcerpt(evidence.Text)
            }
        };

        return new SingleQuestionAnswer(question, answer, citations, subject);
    }

    private static bool IsSubjectOverviewQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains(" la gi", StringComparison.Ordinal)
               || normalized.EndsWith(" la mon gi", StringComparison.Ordinal)
               || normalized.Contains(" mon gi", StringComparison.Ordinal)
               || normalized.Contains("about", StringComparison.Ordinal)
               || normalized.StartsWith("what is ", StringComparison.Ordinal);
    }

    private static string ResolveSubjectName(string subject, string text)
    {
        var syllabusName = Regex.Match(text ?? string.Empty, @"(?im)^\s*Syllabus\s*Name\s*:\s*(?<name>.+?)\s*$", RegexOptions.CultureInvariant);
        if (syllabusName.Success)
        {
            return syllabusName.Groups["name"].Value.Trim();
        }

        var separatorIndex = subject.IndexOf('-', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex + 1 < subject.Length
            ? subject[(separatorIndex + 1)..].Trim()
            : subject.Trim();
    }

    private static ExactFactIntent DetectExactFactIntent(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (normalized.Contains("tin chi", StringComparison.Ordinal)
            || normalized.Contains("credit", StringComparison.Ordinal)
            || normalized.Contains("nocredit", StringComparison.Ordinal))
        {
            return ExactFactIntent.Credits;
        }

        return ExactFactIntent.None;
    }

    private static CreditFact? TryExtractCreditFact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var patterns = new[]
        {
            @"(?im)^\s*No\s*Credit\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b.*$",
            @"(?im)^\s*NoCredit\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b.*$",
            @"(?im)^\s*(?:Credits?|Credit)\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b.*$",
            @"(?im)^\s*(?:So|S[oố])\s*t[ií]n\s*ch[iỉ]\s*:\s*(?<value>\d+(?:[.,]\d+)?)\b.*$",
            @"(?im)^\s*(?<value>\d+(?:[.,]\d+)?)\s*(?:t[ií]n\s*ch[iỉ]|credits?)\b.*$"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var rawValue = match.Groups["value"].Value.Replace(',', '.');
            if (double.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return new CreditFact(value, match.Value.Trim());
            }
        }

        return null;
    }

    private static IReadOnlySet<string> ExtractCourseCodes(string text)
    {
        return Regex.Matches(text ?? string.Empty, @"\b[A-Za-z]{2,}\d{2,}\b", RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveFactCourseLabel(string question, DocumentChunk chunk)
    {
        var code = ExtractCourseCodes(question).FirstOrDefault()
                   ?? ExtractCourseCodes(chunk.Subject).FirstOrDefault();
        return string.IsNullOrWhiteSpace(code) ? chunk.Subject : code;
    }

    private static string FormatCreditValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.0001
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string CreateFactExcerpt(string text, string evidenceText)
    {
        var sourceText = text ?? string.Empty;
        var lines = sourceText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);
        var evidenceIndex = Array.FindIndex(lines, line => line.Equals(evidenceText, StringComparison.OrdinalIgnoreCase));
        if (evidenceIndex < 0)
        {
            return string.IsNullOrWhiteSpace(evidenceText) ? CreateExcerpt(sourceText) : evidenceText;
        }

        var start = Math.Max(0, evidenceIndex - 2);
        var end = Math.Min(lines.Length - 1, evidenceIndex + 2);
        var excerpt = string.Join(" ", lines[start..(end + 1)].Where(line => !string.IsNullOrWhiteSpace(line)));
        return CreateExcerpt(excerpt);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RerankMatchesAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        string language,
        CancellationToken cancellationToken)
    {
        var fallback = RerankLocally(question, candidates);
        if (candidates.Count == 0)
        {
            return fallback;
        }

        if (!_chatCompletionService.IsEnabled)
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
            return Array.Empty<ScoredChunk>();
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
            if (geminiConfidence < MinimumGeminiRerankScore)
            {
                continue;
            }

            var boostedScore = Math.Round(Math.Max(candidate.Score, 0.82 + (geminiConfidence * 0.18)), 3);
            selected.Add(candidate with { Score = boostedScore });
            if (selected.Count == TopK)
            {
                break;
            }
        }

        return selected;
    }

    private static IReadOnlyList<ScoredChunk> RerankLocally(
        string question,
        IReadOnlyList<ScoredChunk> candidates)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<ScoredChunk>();
        }

        var queryTerms = ExtractTerms(question);
        var contentTerms = RemoveCourseScopeTerms(queryTerms);
        var factIntent = DetectExactFactIntent(question);
        var courseCodes = ExtractCourseCodes(question);

        return candidates
            .Select(item =>
            {
                var rerankScore = CalculateLocalRerankScore(item, queryTerms, contentTerms, factIntent, courseCodes);
                return item with { Score = Math.Round(Math.Max(item.Score, rerankScore), 3) };
            })
            .Where(item => HasStrongLocalRerankEvidence(item, contentTerms))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ContentSharedTerms)
            .ThenByDescending(item => item.TextSharedTerms)
            .ThenByDescending(item => item.MetadataSharedTerms)
            .ThenByDescending(item => item.VectorScore)
            .Take(TopK)
            .ToList();
    }

    private static double CalculateLocalRerankScore(
        ScoredChunk candidate,
        IReadOnlySet<string> queryTerms,
        IReadOnlySet<string> contentTerms,
        ExactFactIntent factIntent,
        IReadOnlySet<string> courseCodes)
    {
        var textCoverage = queryTerms.Count == 0 ? 0 : candidate.TextSharedTerms / (double)queryTerms.Count;
        var contentCoverage = contentTerms.Count == 0 ? textCoverage : candidate.ContentSharedTerms / (double)contentTerms.Count;
        var metadataCoverage = queryTerms.Count == 0 ? 0 : Math.Min(1, candidate.MetadataSharedTerms / (double)Math.Min(queryTerms.Count, 4));
        var factBoost = factIntent == ExactFactIntent.Credits && TryExtractCreditFact(candidate.Chunk.Text) is not null ? 0.18 : 0;
        var courseCodeBoost = courseCodes.Count > 0 && CountSharedTerms(courseCodes, BuildChunkMetadataText(candidate.Chunk)) > 0 ? 0.08 : 0;

        return Clamp01(
            (candidate.Score * 0.42)
            + (candidate.LexicalScore * 0.28)
            + (contentCoverage * 0.18)
            + (textCoverage * 0.06)
            + (metadataCoverage * 0.06)
            + factBoost
            + courseCodeBoost);
    }

    private static bool HasStrongLocalRerankEvidence(
        ScoredChunk candidate,
        IReadOnlySet<string> contentTerms)
    {
        if (candidate.Score < MinimumLocalRerankScore)
        {
            return false;
        }

        if (contentTerms.Count == 0)
        {
            return candidate.TextSharedTerms > 0 || candidate.VectorScore >= 0.72;
        }

        var requiredContentTerms = contentTerms.Count >= 3 ? 2 : 1;
        return candidate.ContentSharedTerms >= requiredContentTerms
               || (candidate.ContentSharedTerms > 0 && candidate.TextSharedTerms >= requiredContentTerms)
               || (candidate.MetadataSharedTerms > 0 && candidate.TextSharedTerms > 0 && candidate.VectorScore >= 0.62);
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
        IReadOnlyList<string>? SubjectOptions = null,
        string AnswerSource = "Rag",
        bool HasDirectCitation = true,
        string? FallbackModel = null);

    private enum ExactFactIntent
    {
        None,
        Credits
    }

    private sealed record CreditFact(double Value, string EvidenceText);

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
        int MetadataSharedTerms,
        double VectorScore,
        double LexicalScore);

    private async Task<ChatAnswer> SaveAssistantAnswer(
        Guid sessionId,
        string answer,
        IReadOnlyList<SourceCitation> citations,
        CancellationToken cancellationToken,
        ChatSessionOwnerInfo? ownerInfo = null,
        string? resolvedSubject = null,
        bool needsClarification = false,
        IReadOnlyList<string>? subjectOptions = null,
        string answerSource = "Rag",
        bool hasDirectCitation = true,
        string? fallbackModel = null)
    {
        await _repository.AddMessageAsync(sessionId, new ChatMessage
        {
            Role = "assistant",
            Content = answer,
            Citations = citations.ToList()
        }, cancellationToken, ownerInfo);

        var session = await _repository.GetOrCreateSessionAsync(sessionId, cancellationToken, ownerInfo);
        return new ChatAnswer(
            answer,
            citations,
            session.Messages,
            resolvedSubject,
            needsClarification,
            subjectOptions ?? Array.Empty<string>(),
            answerSource,
            hasDirectCitation,
            fallbackModel);
    }

    private async Task<QueryIntentDecision> ClassifyQuestionIntentAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        string language,
        CancellationToken cancellationToken)
    {
        var localDecision = ClassifyQuestionLocally(question);
        if (localDecision.Intent is ChatQueryIntent.Unsafe or ChatQueryIntent.SmallTalk or ChatQueryIntent.ExternalQuestion)
        {
            return localDecision;
        }

        QueryIntentDecision modelDecision;
        try
        {
            modelDecision = await _chatCompletionService.ClassifyQuestionAsync(question, history, language, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return localDecision;
        }

        if (modelDecision.Confidence < 0.7)
        {
            return localDecision;
        }

        if (HasDocumentQuestionSignal(question)
            && modelDecision.Intent is ChatQueryIntent.SmallTalk or ChatQueryIntent.ExternalQuestion)
        {
            return localDecision;
        }

        return modelDecision;
    }

    private static QueryIntentDecision ClassifyQuestionLocally(string question)
    {
        if (LooksLikePromptInjection(question))
        {
            return new QueryIntentDecision(ChatQueryIntent.Unsafe, 1, "prompt-injection-signal");
        }

        if (IsCasualChat(question) && !HasDocumentQuestionSignal(question))
        {
            return new QueryIntentDecision(ChatQueryIntent.SmallTalk, 0.95, "local-small-talk");
        }

        if (LooksLikeExternalQuestion(question) && !HasDocumentQuestionSignal(question))
        {
            return new QueryIntentDecision(ChatQueryIntent.ExternalQuestion, 0.95, "local-external-question");
        }

        return new QueryIntentDecision(ChatQueryIntent.DocumentQuestion, 0.6, "default-document-question");
    }

    private async Task<IReadOnlyList<string>> BuildRetrievalQueriesAsync(
        string question,
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        var queries = new List<string> { question.Trim() };

        try
        {
            var rewrittenQueries = await _chatCompletionService.RewriteQueriesAsync(question, history, cancellationToken);
            foreach (var rewritten in rewrittenQueries)
            {
                AddDistinctQuery(queries, rewritten);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Retrieval can still run with the original question.
        }

        return queries.Count == 0 ? new[] { question.Trim() } : queries.Take(4).ToList();
    }

    private static void AddDistinctQuery(List<string> queries, string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (!queries.Any(existing => existing.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            queries.Add(trimmed);
        }
    }

    private async Task<bool> IsAnswerGroundedAsync(
        string question,
        string answer,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlySet<string> questionTerms,
        string language,
        CancellationToken cancellationToken)
    {
        var contextText = string.Join("\n\n", chunks.Select(chunk => chunk.Text));
        if (!IsAnswerGrounded(answer, contextText, questionTerms))
        {
            return false;
        }

        GroundingDecision? decision;
        try
        {
            decision = await _chatCompletionService.ValidateGroundingAsync(
                question,
                answer,
                chunks,
                language,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return true;
        }

        return decision is null || decision.IsGrounded || decision.Confidence < 0.65;
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

    private static bool LooksLikeExternalQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return ExternalQuestionSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
    }

    private static bool HasDocumentQuestionSignal(string question)
    {
        var normalized = NormalizeQuestion(question);
        return ExtractCourseCodes(question).Count > 0
               || DocumentQuestionSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal));
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

    private static string BuildExternalScopeAnswer(string language)
    {
        return language == "vi"
            ? "M\u00ecnh ch\u1ec9 h\u1ed7 tr\u1ee3 h\u1ecfi \u0111\u00e1p d\u1ef1a tr\u00ean t\u00e0i li\u1ec7u \u0111\u00e3 index. C\u00e2u n\u00e0y n\u1eb1m ngo\u00e0i ph\u1ea1m vi t\u00e0i li\u1ec7u, n\u00ean m\u00ecnh kh\u00f4ng truy xu\u1ea5t ngu\u1ed3n \u0111\u1ec3 tr\u1ea3 l\u1eddi."
            : "I only answer from indexed documents. This question is outside the document scope, so I will not retrieve sources for it.";
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
                return "Kh\u00f4ng c\u00f3 g\u00ec. C\u1ea7n tra ph\u1ea7n n\u00e0o trong t\u00e0i li\u1ec7u th\u00ec n\u00e9m c\u00e2u h\u1ecfi qua, m\u00ecnh xem ti\u1ebfp.";
            }

            return "You're welcome. Send the next document question over and I will check it.";
        }

        if (normalized.Contains("tam biet", StringComparison.Ordinal)
            || normalized.Contains("bye", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "T\u1ea1m bi\u1ec7t nh\u00e9. Khi c\u1ea7n tra t\u00e0i li\u1ec7u th\u00ec quay l\u1ea1i, m\u00ecnh v\u1eabn \u1edf \u0111\u00e2y.";
            }

            return "Goodbye. Come back when you need to check the documents again.";
        }

        if (normalized.Contains("an com", StringComparison.Ordinal)
            || normalized.Contains("com chua", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "M\u00ecnh kh\u00f4ng \u0103n c\u01a1m \u0111\u01b0\u1ee3c, nh\u01b0ng \u0111ang tr\u1ef1c kho t\u00e0i li\u1ec7u \u0111\u00e2y. B\u1ea1n mu\u1ed1n m\u00ecnh xem m\u00f4n hay ph\u1ea7n n\u00e0o?";
            }

            return "I do not eat, but I am on document duty. What course or section should I check?";
        }

        if (normalized.Contains("khoe khong", StringComparison.Ordinal)
            || normalized.Contains("on khong", StringComparison.Ordinal))
        {
            if (language == "vi")
            {
                return "M\u00ecnh \u1ed5n, \u0111ang s\u1eb5n s\u00e0ng tra t\u00e0i li\u1ec7u. B\u1ea1n \u0111ang c\u1ea7n xem c\u00e2u n\u00e0o?";
            }

            return "I am good and ready to search the documents. What do you want to check?";
        }

        if (language == "vi")
        {
            return "Ch\u00e0o b\u1ea1n, m\u00ecnh \u0111\u00e2y. H\u1ecfi th\u1eb3ng m\u00f4n, CLO, \u0111\u00e1nh gi\u00e1 ho\u1eb7c ph\u1ea7n n\u00e0o trong t\u00e0i li\u1ec7u, m\u00ecnh tra cho.";
        }

        return "Hi, I am here. Ask about a course, CLO, assessment, or document section and I will look it up.";
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
            return "Kh\u00f4ng c\u00f3 g\u00ec. C\u1ea7n tra ph\u1ea7n n\u00e0o trong t\u00e0i li\u1ec7u th\u00ec n\u00e9m c\u00e2u h\u1ecfi qua, m\u00ecnh xem ti\u1ebfp.";
        }

        if (normalized.Contains("tam biet", StringComparison.Ordinal)
            || normalized.Contains("bye", StringComparison.Ordinal))
        {
            return "T\u1ea1m bi\u1ec7t nh\u00e9. Khi c\u1ea7n tra t\u00e0i li\u1ec7u th\u00ec quay l\u1ea1i, m\u00ecnh v\u1eabn \u1edf \u0111\u00e2y.";
        }

        if (normalized.Contains("an com", StringComparison.Ordinal)
            || normalized.Contains("com chua", StringComparison.Ordinal))
        {
            return "M\u00ecnh kh\u00f4ng \u0103n c\u01a1m \u0111\u01b0\u1ee3c, nh\u01b0ng \u0111ang tr\u1ef1c kho t\u00e0i li\u1ec7u \u0111\u00e2y. B\u1ea1n mu\u1ed1n m\u00ecnh xem m\u00f4n hay ph\u1ea7n n\u00e0o?";
        }

        if (normalized.Contains("khoe khong", StringComparison.Ordinal)
            || normalized.Contains("on khong", StringComparison.Ordinal))
        {
            return "M\u00ecnh \u1ed5n, \u0111ang s\u1eb5n s\u00e0ng tra t\u00e0i li\u1ec7u. B\u1ea1n \u0111ang c\u1ea7n xem c\u00e2u n\u00e0o?";
        }

        return "Ch\u00e0o b\u1ea1n, m\u00ecnh \u0111\u00e2y. H\u1ecfi th\u1eb3ng m\u00f4n, CLO, \u0111\u00e1nh gi\u00e1 ho\u1eb7c ph\u1ea7n n\u00e0o trong t\u00e0i li\u1ec7u, m\u00ecnh tra cho.";
    }

    private static string BuildRetrievalQuestion(string originalQuestion, string rewrittenQuestion)
    {
        var original = originalQuestion.Trim();
        var rewritten = string.IsNullOrWhiteSpace(rewrittenQuestion) ? original : rewrittenQuestion.Trim();
        if (original.Equals(rewritten, StringComparison.OrdinalIgnoreCase))
        {
            return original;
        }

        return $"{rewritten}\n{original}";
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
            return "M\u00ecnh c\u00f3 th\u1ea5y v\u00e0i \u00fd li\u00ean quan trong t\u00e0i li\u1ec7u. T\u00f3m g\u1ecdn l\u1ea1i nh\u00e9:\n\n" +
                   string.Join("\n", selectedSentences.Select(sentence => $"- {sentence}")) +
                   "\n\nNgu\u1ed3n m\u00ecnh \u0111\u1ec3 ngay b\u00ean d\u01b0\u1edbi \u0111\u1ec3 b\u1ea1n ki\u1ec3m tra l\u1ea1i.";
        }

        return "I found a few relevant points in the documents. Quick summary:\n\n" +
               string.Join("\n", selectedSentences.Select(sentence => $"- {sentence}")) +
               "\n\nThe sources are listed below so you can double-check them.";
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

        return "M\u00ecnh c\u00f3 th\u1ea5y v\u00e0i \u00fd li\u00ean quan trong t\u00e0i li\u1ec7u. T\u00f3m g\u1ecdn l\u1ea1i nh\u00e9:\n\n" +
               string.Join("\n", selectedSentences.Select(sentence => $"- {sentence}")) +
               "\n\nNgu\u1ed3n m\u00ecnh \u0111\u1ec3 ngay b\u00ean d\u01b0\u1edbi \u0111\u1ec3 b\u1ea1n ki\u1ec3m tra l\u1ea1i.";
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

    private static bool HasRetrievalEvidence(
        ScoredChunk candidate,
        bool needsContentEvidence,
        int minimumSharedTerms)
    {
        if (candidate.Score < MinimumScore)
        {
            return false;
        }

        var hasTextEvidence = candidate.TextSharedTerms >= minimumSharedTerms
                              || candidate.ContentSharedTerms > 0
                              || candidate.VectorScore >= 0.72;
        if (!hasTextEvidence)
        {
            return false;
        }

        return !needsContentEvidence
               || candidate.ContentSharedTerms > 0
               || candidate.TextSharedTerms >= minimumSharedTerms + 1
               || (candidate.MetadataSharedTerms > 0 && candidate.VectorScore >= 0.62);
    }

    private static double CalculateRetrievalScore(double vectorScore, double lexicalScore, bool courseCodeMatched)
    {
        var normalizedVector = Math.Clamp(vectorScore, 0, 1);
        return Clamp01((normalizedVector * 0.46) + (lexicalScore * 0.50) + (courseCodeMatched ? 0.04 : 0));
    }

    private static double CalculateLexicalScore(
        int textSharedTerms,
        int metadataSharedTerms,
        int contentSharedTerms,
        int queryTermCount,
        int contentTermCount)
    {
        if (queryTermCount == 0)
        {
            return 0;
        }

        var textCoverage = textSharedTerms / (double)queryTermCount;
        var contentCoverage = contentTermCount == 0
            ? textCoverage
            : contentSharedTerms / (double)contentTermCount;
        var metadataCoverage = Math.Min(1, metadataSharedTerms / (double)Math.Min(queryTermCount, 4));

        return Clamp01((contentCoverage * 0.56) + (textCoverage * 0.30) + (metadataCoverage * 0.14));
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
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
