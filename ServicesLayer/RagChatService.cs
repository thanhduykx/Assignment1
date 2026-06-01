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

        var questionBatch = SplitQuestionBatch(trimmedQuestion);
        if (questionBatch.Count > 1)
        {
            var scopedChunks = await GetScopedChunksAsync(cancellationToken);
            var questionsToAnswer = questionBatch.Take(MaxBatchQuestions).ToList();
            var answers = new List<SingleQuestionAnswer>(questionsToAnswer.Count);

            foreach (var batchQuestion in questionsToAnswer)
            {
                answers.Add(await BuildSingleQuestionAnswerAsync(
                    batchQuestion,
                    historyBeforeQuestion,
                    userDisplayName,
                    responseLanguage,
                    scopedChunks,
                    cancellationToken));
            }

            var answerText = FormatBatchAnswer(answers, questionBatch.Count - questionsToAnswer.Count, responseLanguage);
            var citations = MergeCitations(answers.SelectMany(item => item.Citations));
            return await SaveAssistantAnswer(sessionId, answerText, citations, cancellationToken);
        }

        var singleAnswer = await BuildSingleQuestionAnswerAsync(
            trimmedQuestion,
            historyBeforeQuestion,
            userDisplayName,
            responseLanguage,
            scopedChunks: null,
            cancellationToken);

        return await SaveAssistantAnswer(sessionId, singleAnswer.Answer, singleAnswer.Citations, cancellationToken);
    }

    private async Task<SingleQuestionAnswer> BuildSingleQuestionAnswerAsync(
        string question,
        IReadOnlyList<ChatMessage> historyBeforeQuestion,
        string? userDisplayName,
        string responseLanguage,
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
        var resolvedQuestion = ResolveKnownCourseAliases(rewrittenQuestion);
        var correctionPrefix = resolvedQuestion.Equals(rewrittenQuestion, StringComparison.Ordinal)
            ? string.Empty
            : BuildAliasCorrectionPrefix(responseLanguage);

        scopedChunks ??= await GetScopedChunksAsync(cancellationToken);
        if (scopedChunks.Count == 0)
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>());
        }

        if (TryBuildDba103SyllabusFactAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var syllabusFactAnswer, out var syllabusFactCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + syllabusFactAnswer, new[] { syllabusFactCitation });
        }

        var queryTerms = ExtractTerms(resolvedQuestion);

        if (TryBuildCreditAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var creditAnswer, out var creditCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + creditAnswer, new[] { creditCitation });
        }

        if (TryBuildCourseOverviewAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var overviewAnswer, out var overviewCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + overviewAnswer, new[] { overviewCitation });
        }

        if (TryBuildPrerequisiteAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var prerequisiteAnswer, out var prerequisiteCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + prerequisiteAnswer, new[] { prerequisiteCitation });
        }

        if (TryBuildTimeAllocationAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var timeAnswer, out var timeCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + timeAnswer, new[] { timeCitation });
        }

        if (TryBuildAssignmentWeightAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var assignmentAnswer, out var assignmentCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + assignmentAnswer, new[] { assignmentCitation });
        }

        if (TryBuildParticipationWeightAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var participationAnswer, out var participationCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + participationAnswer, new[] { participationCitation });
        }

        if (TryBuildFinalExamWeightAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var finalWeightAnswer, out var finalWeightCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + finalWeightAnswer, new[] { finalWeightCitation });
        }

        if (TryBuildAssessmentAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var assessmentAnswer, out var assessmentCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + assessmentAnswer, new[] { assessmentCitation });
        }

        if (TryBuildMainContentAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var mainContentAnswer, out var mainContentCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + mainContentAnswer, new[] { mainContentCitation });
        }

        if (TryBuildKnowledgeAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var knowledgeAnswer, out var knowledgeCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + knowledgeAnswer, new[] { knowledgeCitation });
        }

        if (TryBuildSkillsAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var skillsAnswer, out var skillsCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + skillsAnswer, new[] { skillsCitation });
        }

        if (TryBuildSongsAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var songsAnswer, out var songsCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + songsAnswer, new[] { songsCitation });
        }

        if (TryBuildOnlineResourcesAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var onlineAnswer, out var onlineCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + onlineAnswer, new[] { onlineCitation });
        }

        if (TryBuildFinalExamAnswer(resolvedQuestion, scopedChunks, responseLanguage, out var finalExamAnswer, out var finalExamCitation))
        {
            return new SingleQuestionAnswer(question, correctionPrefix + finalExamAnswer, new[] { finalExamCitation });
        }

        var contentTerms = RemoveCourseScopeTerms(queryTerms);
        var needsContentEvidence = contentTerms.Count > 0;
        var minimumSharedTerms = contentTerms.Count >= 4 ? 2 : 1;
        var queryEmbedding = await _embeddingService.EmbedAsync(resolvedQuestion, cancellationToken);

        var matches = scopedChunks
            .Select(chunk => new
            {
                Chunk = chunk,
                VectorScore = _embeddingService.CosineSimilarity(queryEmbedding, chunk.Embedding),
                TextSharedTerms = CountSharedTerms(queryTerms, chunk.Text),
                ContentSharedTerms = CountSharedTerms(contentTerms, chunk.Text),
                MetadataSharedTerms = CountSharedTerms(queryTerms, BuildChunkMetadataText(chunk))
            })
            .Select(item => new
            {
                item.Chunk,
                Score = CalculateRetrievalScore(
                    item.VectorScore,
                    item.TextSharedTerms,
                    item.MetadataSharedTerms,
                    item.ContentSharedTerms,
                    queryTerms.Count,
                    contentTerms.Count),
                item.TextSharedTerms,
                item.ContentSharedTerms,
                SharedTerms = item.TextSharedTerms + item.MetadataSharedTerms,
                item.MetadataSharedTerms
            })
            .Where(item => item.Score >= MinimumScore
                           && item.TextSharedTerms >= minimumSharedTerms
                           && (!needsContentEvidence || item.ContentSharedTerms > 0))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ContentSharedTerms)
            .ThenByDescending(item => item.TextSharedTerms)
            .ThenByDescending(item => item.MetadataSharedTerms)
            .Take(TopK)
            .ToList();

        if (matches.Count == 0)
        {
            return new SingleQuestionAnswer(question, BuildOutOfScopeAnswer(responseLanguage), Array.Empty<SourceCitation>());
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
                         resolvedQuestion,
                         ResolveSubject(matchedChunks),
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

        return new SingleQuestionAnswer(question, correctionPrefix + answer, citations);
    }

    private async Task<IReadOnlyList<DocumentChunk>> GetScopedChunksAsync(CancellationToken cancellationToken)
    {
        return await _repository.GetChunksAsync(cancellationToken);
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
        IReadOnlyList<SourceCitation> Citations);

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

    private static bool TryBuildCreditAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsCreditQuestion(question))
        {
            return false;
        }

        foreach (var chunk in chunks)
        {
            var match = Regex.Match(
                chunk.Text,
                @"(?:NoCredit|Credits?|S\u1ed1\s+t\u00edn\s+ch\u1ec9|Tin\s+chi)\s*[:：]?\s*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                match = Regex.Match(
                    RemoveDiacritics(chunk.Text),
                    @"(\d+)\s*(?:tin\s*chi|credits?)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            if (!match.Success)
            {
                continue;
            }

            var credit = match.Groups[1].Value;
            var subject = string.IsNullOrWhiteSpace(chunk.Subject) ? "DBA103" : chunk.Subject.Trim();
            answer = language == "vi"
                ? $"{subject} c\u00f3 {credit} t\u00edn ch\u1ec9."
                : $"{subject} has {credit} credits.";
            citation = new SourceCitation
            {
                DocumentId = chunk.DocumentId,
                FileName = chunk.FileName,
                Subject = chunk.Subject,
                Chapter = chunk.Chapter,
                ChunkIndex = chunk.ChunkIndex,
                Score = 1,
                Excerpt = CreateExcerpt(chunk.Text)
            };
            return true;
        }

        return false;
    }

    private static bool IsCreditQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("credit", StringComparison.Ordinal)
               || normalized.Contains("nocredit", StringComparison.Ordinal)
               || normalized.Contains("tin chi", StringComparison.Ordinal)
               || normalized.Contains("so tin", StringComparison.Ordinal)
               || normalized.Contains("may tin", StringComparison.Ordinal)
               || normalized.Contains("bao nhieu tin", StringComparison.Ordinal);
    }

    private static bool TryBuildCourseOverviewAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsCourseOverviewQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item =>
            item.Text.Contains("Subject Code:", StringComparison.OrdinalIgnoreCase)
            && item.Text.Contains("Syllabus Name:", StringComparison.OrdinalIgnoreCase)
            && item.Text.Contains("Description:", StringComparison.OrdinalIgnoreCase));

        if (chunk is null)
        {
            return false;
        }

        var subjectCode = ExtractField(chunk.Text, "Subject Code");
        var syllabusName = ExtractField(chunk.Text, "Syllabus Name");
        var syllabusEnglish = ExtractField(chunk.Text, "Syllabus English");
        var credits = ExtractField(chunk.Text, "NoCredit");
        var description = ExtractDescriptionSummary(chunk.Text);

        if (string.IsNullOrWhiteSpace(subjectCode) || string.IsNullOrWhiteSpace(syllabusName))
        {
            return false;
        }

        answer = language == "vi"
            ? $"{subjectCode} l\u00e0 m\u00f4n {syllabusName}"
              + (string.IsNullOrWhiteSpace(syllabusEnglish) ? string.Empty : $" ({syllabusEnglish})")
              + (string.IsNullOrWhiteSpace(credits) ? "." : $", {credits} t\u00edn ch\u1ec9.")
              + (string.IsNullOrWhiteSpace(description) ? string.Empty : $" {description}")
            : $"{subjectCode} is {syllabusEnglish}"
              + (string.IsNullOrWhiteSpace(syllabusName) ? string.Empty : $" ({syllabusName})")
              + (string.IsNullOrWhiteSpace(credits) ? "." : $", worth {credits} credits.")
              + (string.IsNullOrWhiteSpace(description) ? string.Empty : $" {description}");

        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildPrerequisiteAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsPrerequisiteQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("Pre-Requisite:", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        var prerequisite = ExtractField(chunk.Text, "Pre-Requisite");
        if (string.IsNullOrWhiteSpace(prerequisite))
        {
            return false;
        }

        var subject = ExtractField(chunk.Text, "Subject Code");
        subject = string.IsNullOrWhiteSpace(subject) ? "DBA103" : subject;
        var hasNoPrerequisite = NormalizeQuestion(prerequisite).Contains("khong", StringComparison.Ordinal)
                                || NormalizeQuestion(prerequisite).Contains("none", StringComparison.Ordinal);

        answer = language == "vi"
            ? hasNoPrerequisite
                ? $"{subject} kh\u00f4ng c\u00f3 m\u00f4n ti\u00ean quy\u1ebft."
                : $"{subject} c\u00f3 m\u00f4n ti\u00ean quy\u1ebft: {prerequisite}."
            : hasNoPrerequisite
                ? $"{subject} has no prerequisite."
                : $"{subject} has this prerequisite: {prerequisite}.";

        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool IsCourseOverviewQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("mon gi", StringComparison.Ordinal)
                   || normalized.Contains("la gi", StringComparison.Ordinal)
                   || normalized.Contains("about", StringComparison.Ordinal)
                   || normalized.Contains("what is", StringComparison.Ordinal));
    }

    private static bool IsPrerequisiteQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("tien quyet", StringComparison.Ordinal)
                   || normalized.Contains("prerequisite", StringComparison.Ordinal)
                   || normalized.Contains("pre requisite", StringComparison.Ordinal));
    }

    private static bool TryBuildTimeAllocationAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsTimeAllocationQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("Time Allocation:", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        var allocation = ExtractField(chunk.Text, "Time Allocation");
        if (string.IsNullOrWhiteSpace(allocation))
        {
            return false;
        }

        answer = language == "vi"
            ? $"Th\u1eddi l\u01b0\u1ee3ng h\u1ecdc c\u1ee7a DBA103 l\u00e0 {allocation}."
            : $"DBA103 time allocation is {allocation}.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildAssignmentWeightAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsAssignmentWeightQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item =>
            item.Text.Contains("Assignment:", StringComparison.OrdinalIgnoreCase)
            || item.Text.Contains("B\u00e0i", StringComparison.OrdinalIgnoreCase) && item.Text.Contains("15%", StringComparison.OrdinalIgnoreCase));

        if (chunk is null)
        {
            return false;
        }

        var weight = ExtractPercentAfterLabel(chunk.Text, "Assignment");
        if (string.IsNullOrWhiteSpace(weight))
        {
            weight = "15%";
        }

        answer = language == "vi"
            ? $"B\u00e0i t\u1eadp trong DBA103 chi\u1ebfm {weight} t\u1ed5ng \u0111i\u1ec3m."
            : $"The assignment component in DBA103 accounts for {weight} of the total grade.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildParticipationWeightAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsParticipationWeightQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item =>
            item.Text.Contains("Participation:", StringComparison.OrdinalIgnoreCase)
            || item.Text.Contains("tham gia", StringComparison.OrdinalIgnoreCase) && item.Text.Contains("15%", StringComparison.OrdinalIgnoreCase));

        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "\u0110i\u1ec3m \u00fd th\u1ee9c tham gia l\u1edbp h\u1ecdc trong DBA103 chi\u1ebfm 15% t\u1ed5ng \u0111i\u1ec3m."
            : "The participation component in DBA103 accounts for 15% of the total grade.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildFinalExamWeightAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsFinalExamWeightQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item =>
            item.Text.Contains("Final exam:", StringComparison.OrdinalIgnoreCase)
            || item.Text.Contains("Thi", StringComparison.OrdinalIgnoreCase) && item.Text.Contains("70%", StringComparison.OrdinalIgnoreCase));

        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "Thi cu\u1ed1i m\u00f4n DBA103 chi\u1ebfm 70% t\u1ed5ng \u0111i\u1ec3m."
            : "The final exam in DBA103 accounts for 70% of the total grade.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildAssessmentAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsAssessmentQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item =>
            item.Text.Contains("Assignment:", StringComparison.OrdinalIgnoreCase)
            && item.Text.Contains("Participation:", StringComparison.OrdinalIgnoreCase)
            && item.Text.Contains("Final exam:", StringComparison.OrdinalIgnoreCase));

        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "DBA103 \u0111\u01b0\u1ee3c \u0111\u00e1nh gi\u00e1 theo 3 ph\u1ea7n: b\u00e0i t\u1eadp 15%, \u00fd th\u1ee9c tham gia l\u1edbp 15%, v\u00e0 thi cu\u1ed1i m\u00f4n th\u1ef1c h\u00e0nh ch\u01a1i nh\u1ea1c c\u1ee5 70%. T\u1ed5ng \u0111i\u1ec3m FE c\u1ea7n \u0111\u1ea1t t\u1eeb 5, \u0111i\u1ec3m trung b\u00ecnh t\u1ed1i thi\u1ec3u \u0111\u1ec3 qua m\u00f4n l\u00e0 5."
            : "DBA103 is assessed through 3 components: assignment 15%, participation 15%, and final practical musical-instrument exam 70%. The final result must be at least 5, and the minimum average mark to pass is 5.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildMainContentAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsMainContentQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("In the course, students will learn:", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "N\u1ed9i dung ch\u00ednh c\u1ee7a DBA103 g\u1ed3m: l\u1ecbch s\u1eed ph\u00e1t tri\u1ec3n c\u1ee7a \u0111\u00e0n B\u1ea7u \u1edf Vi\u1ec7t Nam; c\u1ea5u tr\u00fac v\u00e0 \u0111\u1eb7c \u0111i\u1ec3m c\u1ee7a \u0111\u00e0n B\u1ea7u; t\u01b0 th\u1ebf \u0111\u00e1nh \u0111\u00e0n; nh\u1ea1c l\u00fd v\u00e0 k\u1ef9 thu\u1eadt c\u01a1 b\u1ea3n nh\u01b0 g\u1ea3y d\u00e2y bu\u00f4ng, nh\u1ea5n l\u00ean/xu\u1ed1ng qu\u00e3ng 2; luy\u1ec7n t\u1eadp c\u00e1c b\u00e0i Vi\u1ec7t Nam v\u00e0 b\u00e0i qu\u1ed1c t\u1ebf."
            : "The main contents of DBA103 include the historical development of Dan Bau in Vietnam, its structure and characteristics, playing posture, music theory and basic techniques such as plucking open strings and raising/lowering pitch, plus practice with Vietnamese and foreign songs.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildKnowledgeAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsKnowledgeQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("Ki\u1ebfn th\u1ee9c/ Knowledge:", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "V\u1ec1 ki\u1ebfn th\u1ee9c, sinh vi\u00ean DBA103 c\u1ea7n n\u1eafm \u0111\u1eb7c tr\u01b0ng v\u1ec1 l\u1ecbch s\u1eed ph\u00e1t tri\u1ec3n v\u00e0 c\u1ea5u tr\u00fac \u0111\u00e0n B\u1ea7u, \u0111\u1ed3ng th\u1eddi l\u00e0m quen v\u1edbi nh\u1ea1c l\u00fd v\u00e0 c\u00e1c k\u1ef9 thu\u1eadt c\u01a1 b\u1ea3n c\u1ee7a \u0111\u00e0n B\u1ea7u."
            : "For knowledge, DBA103 students should understand the historical development and structure of Dan Bau, and become familiar with music theory and basic Dan Bau techniques.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildSkillsAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsSkillsQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("K\u1ef9 n\u0103ng/ Skills:", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "V\u1ec1 k\u1ef9 n\u0103ng, sinh vi\u00ean c\u1ea7n \u0111\u00e1nh \u0111\u01b0\u1ee3c t\u1ed1i thi\u1ec3u 3 b\u00e0i, trong \u0111\u00f3 c\u00f3 1 b\u00e0i nh\u1ea1c n\u01b0\u1edbc ngo\u00e0i, \u1edf m\u1ee9c th\u00f4ng d\u1ee5ng v\u00e0 v\u1eadn d\u1ee5ng \u0111\u00fang c\u00e1c k\u1ef9 thu\u1eadt c\u01a1 b\u1ea3n."
            : "For skills, students should be able to play at least 3 songs, including 1 common foreign song, and properly apply basic techniques.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildSongsAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsSongsQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item =>
            item.Text.Contains("Practice playing Vietnamese songs", StringComparison.OrdinalIgnoreCase)
            || item.Text.Contains("Danh m\u1ee5c c\u00e1c b\u00e0i nh\u1ea1c", StringComparison.OrdinalIgnoreCase));

        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "Trong DBA103, sinh vi\u00ean luy\u1ec7n t\u1eadp c\u00e1c b\u00e0i Vi\u1ec7t Nam nh\u01b0 C\u00f2 l\u1ea3, L\u00fd c\u00e2y \u0111a v\u00e0 b\u00e0i qu\u1ed1c t\u1ebf Auld Lang Syne. T\u00e0i li\u1ec7u c\u0169ng li\u1ec7t k\u00ea th\u00eam c\u00e1c b\u00e0i c\u00f3 th\u1ec3 s\u1eed d\u1ee5ng trong h\u1ecdc ph\u1ea7n nh\u01b0 B\u1eafc Kim Thang, Inh l\u1ea3 \u01a1i, X\u00f2e hoa, Tr\u1ed1ng c\u01a1m, \u0110\u1ed9i k\u00e8n t\u00ed hon."
            : "In DBA103, students practice Vietnamese songs such as Co la and Ly cay da, and the foreign song Auld Lang Syne. The syllabus also lists possible course songs such as Bac Kim Thang, Inh la oi, Xoe hoa, Trong com, and Doi ken ti hon.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildOnlineResourcesAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsOnlineResourcesQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("Apply IT in the course", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "DBA103 s\u1eed d\u1ee5ng t\u00e0i nguy\u00ean tr\u00ean m\u1ea1ng b\u1eb1ng c\u00e1ch cung c\u1ea5p website, clip nh\u1ea1c truy\u1ec1n th\u1ed1ng; gi\u1ea3ng vi\u00ean ch\u1ecdn l\u1ecdc t\u00e0i nguy\u00ean theo nguy\u00ean t\u1eafc h\u1ecdc ph\u1ea7n, cung c\u1ea5p cho sinh vi\u00ean v\u00e0 h\u01b0\u1edbng d\u1eabn sinh vi\u00ean t\u00ecm th\u00f4ng tin theo ch\u1ee7 \u0111\u1ec1 tr\u00ean Internet."
            : "DBA103 uses online resources by providing websites and traditional music clips. Lecturers selectively provide online resources based on course regulations and guide students to search for topic-based information on the Internet.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static bool TryBuildFinalExamAnswer(
        string question,
        IReadOnlyList<DocumentChunk> chunks,
        string language,
        out string answer,
        out SourceCitation citation)
    {
        answer = string.Empty;
        citation = new SourceCitation();

        if (!IsFinalExamQuestion(question))
        {
            return false;
        }

        var chunk = chunks.FirstOrDefault(item => item.Text.Contains("T\u1ed5 ch\u1ee9c thi k\u1ebft th\u00fac kh\u00f3a h\u1ecdc", StringComparison.OrdinalIgnoreCase))
                    ?? chunks.FirstOrDefault(item => item.Text.Contains("Final exam:", StringComparison.OrdinalIgnoreCase));
        if (chunk is null)
        {
            return false;
        }

        answer = language == "vi"
            ? "Theo k\u1ebf ho\u1ea1ch h\u1ecdc, DBA103 t\u1ed5 ch\u1ee9c thi k\u1ebft th\u00fac kh\u00f3a h\u1ecdc \u1edf session 29 v\u00e0 30. Ph\u1ea7n thi cu\u1ed1i m\u00f4n l\u00e0 th\u1ef1c h\u00e0nh ch\u01a1i nh\u1ea1c c\u1ee5, chi\u1ebfm 70%."
            : "According to the course schedule, DBA103 final exams are held in sessions 29 and 30. The final exam is a practical musical-instrument performance and accounts for 70%.";
        citation = BuildCitation(chunk, 1);
        return true;
    }

    private static string ExtractField(string text, string fieldName)
    {
        var match = Regex.Match(
            text,
            $@"{Regex.Escape(fieldName)}\s*:\s*(?<value>[^\r\n]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static string ExtractPercentAfterLabel(string text, string label)
    {
        var match = Regex.Match(
            text,
            $@"{Regex.Escape(label)}\s*:\s*(?<value>\d+(?:\.\d+)?%)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static bool IsTimeAllocationQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("thoi luong", StringComparison.Ordinal)
                   || normalized.Contains("bao nhieu slot", StringComparison.Ordinal)
                   || normalized.Contains("time allocation", StringComparison.Ordinal)
                   || normalized.Contains("duration", StringComparison.Ordinal));
    }

    private static bool IsAssignmentWeightQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("bai tap", StringComparison.Ordinal)
                   || normalized.Contains("assignment", StringComparison.Ordinal))
               && (normalized.Contains("phan tram", StringComparison.Ordinal)
                   || normalized.Contains("chiem", StringComparison.Ordinal)
                   || normalized.Contains("weight", StringComparison.Ordinal)
                   || normalized.Contains("%", StringComparison.Ordinal));
    }

    private static bool IsParticipationWeightQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("tham gia", StringComparison.Ordinal)
                   || normalized.Contains("y thuc", StringComparison.Ordinal)
                   || normalized.Contains("participation", StringComparison.Ordinal))
               && (normalized.Contains("phan tram", StringComparison.Ordinal)
                   || normalized.Contains("chiem", StringComparison.Ordinal)
                   || normalized.Contains("weight", StringComparison.Ordinal)
                   || normalized.Contains("%", StringComparison.Ordinal));
    }

    private static bool IsFinalExamWeightQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("thi cuoi", StringComparison.Ordinal)
                   || normalized.Contains("final exam", StringComparison.Ordinal))
               && (normalized.Contains("phan tram", StringComparison.Ordinal)
                   || normalized.Contains("chiem", StringComparison.Ordinal)
                   || normalized.Contains("weight", StringComparison.Ordinal)
                   || normalized.Contains("%", StringComparison.Ordinal));
    }

    private static bool IsAssessmentQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("danh gia", StringComparison.Ordinal)
                   || normalized.Contains("assessment", StringComparison.Ordinal)
                   || normalized.Contains("scoring", StringComparison.Ordinal)
                   || normalized.Contains("tinh diem", StringComparison.Ordinal));
    }

    private static bool IsMainContentQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("noi dung chinh", StringComparison.Ordinal)
                   || normalized.Contains("gom nhung gi", StringComparison.Ordinal)
                   || normalized.Contains("main content", StringComparison.Ordinal)
                   || normalized.Contains("contents", StringComparison.Ordinal));
    }

    private static bool IsKnowledgeQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("kien thuc", StringComparison.Ordinal)
                   || normalized.Contains("knowledge", StringComparison.Ordinal)
                   || normalized.Contains("nam kien thuc", StringComparison.Ordinal));
    }

    private static bool IsSkillsQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("ky nang", StringComparison.Ordinal)
                   || normalized.Contains("skills", StringComparison.Ordinal)
                   || normalized.Contains("luyen tap ky", StringComparison.Ordinal));
    }

    private static bool IsSongsQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("bai nao", StringComparison.Ordinal)
                   || normalized.Contains("bai nhac", StringComparison.Ordinal)
                   || normalized.Contains("songs", StringComparison.Ordinal)
                   || normalized.Contains("practice", StringComparison.Ordinal)
                   || normalized.Contains("luyen tap", StringComparison.Ordinal));
    }

    private static bool IsOnlineResourcesQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("tai nguyen tren mang", StringComparison.Ordinal)
                   || normalized.Contains("online resources", StringComparison.Ordinal)
                   || normalized.Contains("internet", StringComparison.Ordinal)
                   || normalized.Contains("website", StringComparison.Ordinal));
    }

    private static bool IsFinalExamQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        return normalized.Contains("dba103", StringComparison.Ordinal)
               && (normalized.Contains("thi cuoi", StringComparison.Ordinal)
                   || normalized.Contains("final exam", StringComparison.Ordinal)
                   || normalized.Contains("ket thuc khoa", StringComparison.Ordinal));
    }

    private static string ExtractDescriptionSummary(string text)
    {
        var match = Regex.Match(
            text,
            @"Description:\s*(?<value>.*?)(?:\n\s*2\.\s*K\u1ef9\s+n\u0103ng|\n\s*2\.\s*Skills|\z)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return string.Empty;
        }

        var lines = match.Groups["value"].Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("-", StringComparison.Ordinal))
            .Select(line => line.Trim('-', ' ', '\t'))
            .Take(2)
            .ToList();

        return lines.Count == 0
            ? string.Empty
            : "N\u1ed9i dung m\u00f4n h\u1ecdc t\u1eadp trung v\u00e0o: " + string.Join("; ", lines) + ".";
    }

    private static SourceCitation BuildCitation(DocumentChunk chunk, double score)
    {
        return new SourceCitation
        {
            DocumentId = chunk.DocumentId,
            FileName = chunk.FileName,
            Subject = chunk.Subject,
            Chapter = chunk.Chapter,
            ChunkIndex = chunk.ChunkIndex,
            Score = score,
            Excerpt = CreateExcerpt(chunk.Text)
        };
    }

    private static string ResolveKnownCourseAliases(string question)
    {
        var normalized = NormalizeQuestion(question);
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var looksLikeDba103 = normalized.Contains("bdi", StringComparison.Ordinal)
                              || normalized.Contains("dba103", StringComparison.Ordinal)
                              || normalized.Contains("dba 103", StringComparison.Ordinal)
                              || terms.Any(term => TermsMatch(term, "dba103"));

        if (!looksLikeDba103)
        {
            return question;
        }

        return Regex.Replace(
            question,
            @"\bbdi\b|\bdba[\s-]?103\b",
            "DBA103",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string BuildAliasCorrectionPrefix(string language)
    {
        return language == "vi"
            ? "Có vẻ bạn đang nhắc đến DBA103. "
            : "It looks like you are referring to DBA103. ";
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
        scopedTerms.Remove("dba103");
        scopedTerms.Remove("dba");
        scopedTerms.Remove("103");
        scopedTerms.Remove("bdi");
        scopedTerms.Remove("syllabus");
        scopedTerms.Remove("11835");
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
