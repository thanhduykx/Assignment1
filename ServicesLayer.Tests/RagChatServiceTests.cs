using DataAccessLayer;
using ServicesLayer;

namespace ServicesLayer.Tests;

public sealed class RagChatServiceTests
{
    [Fact]
    public async Task AskAsync_ReturnsExactCreditFact_WhenNoCreditFieldExists()
    {
        var documentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Syllabus ID: 11835
                    Syllabus Name: Nhac cu truyen thong - Dan Bau
                    Subject Code: DBA103
                    NoCredit: 3
                    Degree Level: Beginner
                    """,
                Embedding = new Dictionary<int, double> { [1] = 1 }
            },
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 3,
                Text = "Cung cap thong tin, tai nguyen len mang va clip nhac truyen thong.",
                Embedding = new Dictionary<int, double> { [2] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 co bao nhieu tin chi?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.StartsWith("M\u00ecnh xem trong t\u00e0i li\u1ec7u r\u1ed3i:", answer.Answer);
        Assert.Contains("DBA103 c\u00f3 3 t\u00edn ch\u1ec9", answer.Answer);
        Assert.Single(answer.Citations);
        Assert.Equal(1, answer.Citations[0].ChunkIndex);
        Assert.Contains("NoCredit: 3", answer.Citations[0].Excerpt);
    }

    [Fact]
    public async Task AskAsync_UsesOriginalQuestion_WhenRewriteDropsCourseCode()
    {
        var dbaDocumentId = Guid.NewGuid();
        var pruDocumentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = pruDocumentId,
                FileName = "FLM-Syllabus-PRU.txt",
                Subject = "PRU - UNITY",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Subject Code: PRU
                    Syllabus Name: UNITY
                    NoCredit: 4
                    """,
                Embedding = new Dictionary<int, double> { [1] = 1 }
            },
            new DocumentChunk
            {
                DocumentId = dbaDocumentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Subject Code: DBA103
                    Syllabus Name: Nhac cu truyen thong - Dan Bau
                    NoCredit: 3
                    """,
                Embedding = new Dictionary<int, double> { [2] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService("How many credits does this course have?"));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 co bao nhieu tin chi?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau", "PRU - UNITY" });

        Assert.StartsWith("M\u00ecnh xem trong t\u00e0i li\u1ec7u r\u1ed3i:", answer.Answer);
        Assert.Contains("DBA103 c\u00f3 3 t\u00edn ch\u1ec9", answer.Answer);
        Assert.Single(answer.Citations);
        Assert.Equal("DBA103 - Nhac cu truyen thong - Dan Bau", answer.Citations[0].Subject);
    }

    [Fact]
    public async Task AskAsync_LocalRerankPrefersDirectEvidence_WhenVectorScoreIsMisleading()
    {
        var documentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = "Final project resources: students upload music clips and presentation material.",
                Embedding = new Dictionary<int, double> { [1] = 1 }
            },
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Assessment",
                ChunkIndex = 2,
                Text = "Assessment: final exam percentage is 70%. Participation is 15%. Quiz is 15%.",
                Embedding = new Dictionary<int, double> { [2] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new BiasedQueryEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 final exam percentage?",
            language: "en",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.NotEmpty(answer.Citations);
        Assert.Equal(2, answer.Citations[0].ChunkIndex);
        Assert.Contains("70%", answer.Answer);
    }

    [Fact]
    public async Task AskAsync_ExplicitSubjectCodeOverridesSelectedSubjectFilter()
    {
        var dbaDocumentId = Guid.NewGuid();
        var iotDocumentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = iotDocumentId,
                FileName = "FLM-Syllabus-12400-IOT102.txt",
                Subject = "IOT102 - Internet of Things",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Subject Code: IOT102
                    Syllabus Name: Internet of Things
                    NoCredit: 3
                    """,
                Embedding = new Dictionary<int, double> { [1] = 1 }
            },
            new DocumentChunk
            {
                DocumentId = dbaDocumentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Subject Code: DBA103
                    Syllabus Name: Nhac cu truyen thong - Dan Bau
                    NoCredit: 3
                    """,
                Embedding = new Dictionary<int, double> { [2] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "dba103 co bao nhieu tin chi",
            subjectFilter: "IOT102 - Internet of Things",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau", "IOT102 - Internet of Things" });

        Assert.StartsWith("M\u00ecnh xem trong t\u00e0i li\u1ec7u r\u1ed3i:", answer.Answer);
        Assert.Contains("DBA103 c\u00f3 3 t\u00edn ch\u1ec9", answer.Answer);
        Assert.Single(answer.Citations);
        Assert.Equal("DBA103 - Nhac cu truyen thong - Dan Bau", answer.Citations[0].Subject);
        Assert.Equal("FLM-Syllabus-11835-DBA103.txt", answer.Citations[0].FileName);
    }

    [Fact]
    public async Task AskAsync_AnswersSubjectOverview_WhenQuestionUsesUniqueCodePrefix()
    {
        var dbaDocumentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = dbaDocumentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = """
                    Subject Code: DBA103
                    Syllabus Name: Nhac cu truyen thong - Dan Bau
                    NoCredit: 3
                    """,
                Embedding = new Dictionary<int, double> { [1] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "Dba la gi?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Contains("DBA103", answer.Answer);
        Assert.Contains("Nhac cu truyen thong - Dan Bau", answer.Answer);
        Assert.Single(answer.Citations);
        Assert.Equal("DBA103 - Nhac cu truyen thong - Dan Bau", answer.Citations[0].Subject);
    }

    [Fact]
    public async Task AskAsync_DoesNotRetrieveChunks_ForExternalQuestion()
    {
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = Guid.NewGuid(),
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = "Subject Code: DBA103 NoCredit: 3",
                Embedding = new Dictionary<int, double> { [1] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "thoi tiet hom nay nhu nao?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Contains("t\u00e0i li\u1ec7u", answer.Answer);
        Assert.Empty(answer.Citations);
        Assert.Equal(0, repository.GetChunksCallCount);
    }

    [Fact]
    public async Task AskAsync_ReturnsSmallTalkWithoutCitations()
    {
        var repository = new InMemoryChatKnowledgeRepository(Array.Empty<DocumentChunk>());
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "an com chua?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Empty(answer.Citations);
        Assert.Equal(0, repository.GetChunksCallCount);
        Assert.Contains("tr\u1ef1c kho t\u00e0i li\u1ec7u", answer.Answer);
    }

    [Fact]
    public async Task AskAsync_DoesNotFallback_WhenEnabledRerankerSelectsNoEvidence()
    {
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = Guid.NewGuid(),
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = "DBA103 students practice music clips and upload learning resources.",
                Embedding = new HashingEmbeddingService().EmbedWithHashing("DBA103 assessment final exam")
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService(isEnabled: true, rerankResults: Array.Empty<ChatChunkRerankResult>()));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 assessment final exam?",
            language: "en",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Equal("I do not have enough data in the documents to answer this question.", answer.Answer);
        Assert.Empty(answer.Citations);
    }

    [Fact]
    public async Task AskAsync_RejectsGeneratedAnswer_WhenGroundingFails()
    {
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = Guid.NewGuid(),
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Assessment",
                ChunkIndex = 2,
                Text = "Assessment: final exam percentage is 70%. Participation is 15%. Quiz is 15%.",
                Embedding = new HashingEmbeddingService().EmbedWithHashing("DBA103 final exam percentage")
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService(
                isEnabled: true,
                rerankResults: new[] { new ChatChunkRerankResult(1, 0.95, "direct") },
                generatedAnswer: "DBA103 final exam is 80%.",
                groundingDecision: new GroundingDecision(false, 0.9, "80% is not in evidence")));

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 final exam percentage?",
            language: "en",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Equal("I do not have enough data in the documents to answer this question.", answer.Answer);
        Assert.Empty(answer.Citations);
    }

    [Fact]
    public async Task AskAsync_DoesNotUseFineTunedFallback_WhenRagHasNoEvidence()
    {
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = Guid.NewGuid(),
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                ChunkIndex = 1,
                Text = "This chunk only describes general class logistics.",
                Embedding = new Dictionary<int, double> { [1] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 co chuan dau ra nao?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Equal("OutOfScope", answer.AnswerSource);
        Assert.False(answer.HasDirectCitation);
        Assert.Null(answer.FallbackModel);
        Assert.Empty(answer.Citations);
        Assert.Equal("M\u00ecnh kh\u00f4ng \u0111\u1ee7 d\u1eef li\u1ec7u trong t\u00e0i li\u1ec7u \u0111\u1ec3 tr\u1ea3 l\u1eddi c\u00e2u h\u1ecfi n\u00e0y.", answer.Answer);
    }

    [Fact]
    public async Task AskAsync_UsesDocumentChunksWithoutFineTunedFallback()
    {
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = Guid.NewGuid(),
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Assessment",
                ChunkIndex = 8,
                Text = "Assessment details: Participation 15%. Quiz 15%. Final practical exam 70%.",
                Embedding = new Dictionary<int, double> { [1] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 co ty le thi hoac danh gia nao?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Equal("Rag", answer.AnswerSource);
        Assert.True(answer.HasDirectCitation);
        Assert.NotEmpty(answer.Citations);
        Assert.Contains("70%", answer.Answer);
    }

    [Fact]
    public async Task AskAsync_AnswersIndexedChaptersFromMetadata()
    {
        var documentId = Guid.NewGuid();
        var repository = new InMemoryChatKnowledgeRepository(new[]
        {
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Tong Quan",
                SectionTitle = "Syllabus 11835",
                ChunkIndex = 1,
                Text = "Subject Code: DBA103. Syllabus Name: Nhac cu truyen thong - Dan Bau.",
                Embedding = new Dictionary<int, double> { [1] = 1 }
            },
            new DocumentChunk
            {
                DocumentId = documentId,
                FileName = "FLM-Syllabus-11835-DBA103.txt",
                Subject = "DBA103 - Nhac cu truyen thong - Dan Bau",
                Chapter = "Assessment",
                SectionTitle = "Assessment",
                ChunkIndex = 2,
                Text = "Assessment details: Participation 15%. Quiz 15%. Final practical exam 70%.",
                Embedding = new Dictionary<int, double> { [2] = 1 }
            }
        });
        var service = new RagChatService(
            repository,
            new HashingEmbeddingService(),
            new NoOpChatCompletionService());

        var answer = await service.AskAsync(
            Guid.NewGuid(),
            "DBA103 da index nhung chuong hoac phan nao?",
            language: "vi",
            allowedSubjects: new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

        Assert.Equal("Rag", answer.AnswerSource);
        Assert.Contains("Tong Quan", answer.Answer);
        Assert.Contains("Assessment", answer.Answer);
        Assert.NotEmpty(answer.Citations);
    }

    [Fact]
    public async Task FineTunedChatService_DoesNotUseExampleFromWrongSubject()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fine-tuned-chat-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
            {
              "examples": [
                {
                  "subject": "IOT102 - Internet of Things",
                  "status": "Approved",
                  "question": "IOT102 co chuan dau ra nao?",
                  "answer": "IOT102 co chuan dau ra ve cam bien va ket noi IoT."
                }
              ]
            }
            """);
        try
        {
            var service = new FineTunedChatService(
                researchRepository: null,
                new HttpClient(),
                new FineTunedChatOptions(true, "local://supervised-qa", "local://supervised-qa", 0.62, false, path));

            var answer = await service.TryAnswerAsync(
                "DBA103 co chuan dau ra nao?",
                "DBA103 - Nhac cu truyen thong - Dan Bau",
                Array.Empty<ChatMessage>(),
                new[] { "DBA103 - Nhac cu truyen thong - Dan Bau", "IOT102 - Internet of Things" });

            Assert.Null(answer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FineTunedChatService_RejectsLowConfidenceMatch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fine-tuned-chat-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """
            {
              "examples": [
                {
                  "subject": "DBA103 - Nhac cu truyen thong - Dan Bau",
                  "status": "Approved",
                  "question": "DBA103 co bao nhieu tin chi?",
                  "answer": "DBA103 co 3 tin chi."
                }
              ]
            }
            """);
        try
        {
            var service = new FineTunedChatService(
                researchRepository: null,
                new HttpClient(),
                new FineTunedChatOptions(true, "local://supervised-qa", "local://supervised-qa", 0.9, false, path));

            var answer = await service.TryAnswerAsync(
                "DBA103 sinh vien can lam gi?",
                "DBA103 - Nhac cu truyen thong - Dan Bau",
                Array.Empty<ChatMessage>(),
                new[] { "DBA103 - Nhac cu truyen thong - Dan Bau" });

            Assert.Null(answer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class NoOpChatCompletionService : ILocalChatCompletionService
    {
        private readonly string? _rewrittenQuestion;
        private readonly IReadOnlyList<ChatChunkRerankResult> _rerankResults;
        private readonly string? _generatedAnswer;
        private readonly GroundingDecision? _groundingDecision;

        public NoOpChatCompletionService(
            string? rewrittenQuestion = null,
            bool isEnabled = false,
            IReadOnlyList<ChatChunkRerankResult>? rerankResults = null,
            string? generatedAnswer = null,
            GroundingDecision? groundingDecision = null)
        {
            _rewrittenQuestion = rewrittenQuestion;
            IsEnabled = isEnabled;
            _rerankResults = rerankResults ?? Array.Empty<ChatChunkRerankResult>();
            _generatedAnswer = generatedAnswer;
            _groundingDecision = groundingDecision;
        }

        public bool IsEnabled { get; }

        public Task<QueryIntentDecision> ClassifyQuestionAsync(
            string question,
            IReadOnlyList<ChatMessage> history,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new QueryIntentDecision(ChatQueryIntent.DocumentQuestion, 0, "test-noop"));
        }

        public Task<string> RewriteQuestionAsync(
            string question,
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rewrittenQuestion ?? question);
        }

        public Task<IReadOnlyList<string>> RewriteQueriesAsync(
            string question,
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> queries = string.IsNullOrWhiteSpace(_rewrittenQuestion)
                ? new[] { question }
                : new[] { _rewrittenQuestion };
            return Task.FromResult(queries);
        }

        public Task<IReadOnlyList<ChatChunkRerankResult>> RerankChunksAsync(
            string question,
            IReadOnlyList<DocumentChunk> chunks,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rerankResults);
        }

        public Task<string?> GenerateAnswerAsync(
            string question,
            string subject,
            IReadOnlyList<ChatMessage> history,
            IReadOnlyList<DocumentChunk> chunks,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_generatedAnswer);
        }

        public Task<GroundingDecision?> ValidateGroundingAsync(
            string question,
            string answer,
            IReadOnlyList<DocumentChunk> chunks,
            string language,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_groundingDecision);
        }
    }

    private sealed class BiasedQueryEmbeddingService : IEmbeddingService
    {
        public string ModelName => "biased-query-test";
        public int Dimensions => 2;

        public Task<Dictionary<int, double>> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Dictionary<int, double> { [1] = 1 });
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
    }

    private sealed class InMemoryChatKnowledgeRepository : IKnowledgeRepository
    {
        private readonly IReadOnlyList<DocumentChunk> _chunks;
        private readonly Dictionary<Guid, ChatSession> _sessions = new();

        public InMemoryChatKnowledgeRepository(IReadOnlyList<DocumentChunk> chunks)
        {
            _chunks = chunks;
        }

        public int GetChunksCallCount { get; private set; }

        public Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
        {
            GetChunksCallCount++;
            return Task.FromResult(_chunks);
        }

        public Task<ChatSession> GetOrCreateSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default,
            ChatSessionOwnerInfo? ownerInfo = null)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                session = new ChatSession { Id = sessionId };
                _sessions[sessionId] = session;
            }

            return Task.FromResult(session);
        }

        public Task AddMessageAsync(
            Guid sessionId,
            ChatMessage message,
            CancellationToken cancellationToken = default,
            ChatSessionOwnerInfo? ownerInfo = null)
        {
            var session = _sessions.GetValueOrDefault(sessionId) ?? new ChatSession { Id = sessionId };
            session.Messages.Add(message);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            _sessions[sessionId] = session;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IndexedDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IndexedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IndexedDocument>> GetDocumentsByStatusAsync(string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentChunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddDocumentAsync(IndexedDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDocumentIndexProcessingAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteDocumentIndexAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, string embeddingModel, int embeddingDimensions, string chunkingStrategy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkDocumentIndexFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IndexedDocument> UpdateDocumentMetadataAsync(Guid documentId, string fileName, string subject, string chapter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CourseSubject>> GetCourseCatalogAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CourseSubject> UpsertSubjectAsync(Guid? subjectId, string code, string name, string? description, CancellationToken cancellationToken = default, SubjectOwnerInfo? ownerInfo = null) => throw new NotSupportedException();
        public Task DeleteSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CourseChapter> UpsertChapterAsync(Guid? chapterId, Guid subjectId, string title, int sortOrder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatSession>> GetSessionsForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatSession?> GetSessionForOwnerAsync(Guid sessionId, Guid ownerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatSession?> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) => throw new NotSupportedException();
        public Task<ChatSession?> SetSessionStarredAsync(Guid sessionId, bool isStarred, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) => throw new NotSupportedException();
        public Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default, ChatSessionOwnerInfo? ownerInfo = null) => throw new NotSupportedException();
    }
}
