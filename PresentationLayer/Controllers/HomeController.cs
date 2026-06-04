using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using DataAccessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Models;
using PresentationLayer.Security;
using PresentationLayer.Services;
using ServicesLayer;

namespace PresentationLayer.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IKnowledgeRepository _repository;
        private readonly IDocumentIndexingService _indexingService;
        private readonly IWebPageTextExtractor _webPageTextExtractor;
        private readonly IRagChatService _chatService;
        private readonly IUserAccountStore _users;
        private readonly IWebHostEnvironment _environment;
        private readonly IDocumentIndexJobQueue _indexJobQueue;

        public HomeController(
            ILogger<HomeController> logger,
            IKnowledgeRepository repository,
            IDocumentIndexingService indexingService,
            IWebPageTextExtractor webPageTextExtractor,
            IRagChatService chatService,
            IUserAccountStore users,
            IWebHostEnvironment environment,
            IDocumentIndexJobQueue indexJobQueue)
        {
            _logger = logger;
            _repository = repository;
            _indexingService = indexingService;
            _webPageTextExtractor = webPageTextExtractor;
            _chatService = chatService;
            _users = users;
            _environment = environment;
            _indexJobQueue = indexJobQueue;
        }

        [Authorize(Policy = AuthorizationPolicies.DocumentRead)]
        public async Task<IActionResult> Index(string? subjectFilter, CancellationToken cancellationToken)
        {
            var allDocuments = await _indexingService.GetDocumentsAsync(cancellationToken);
            if (CanManageDocuments())
            {
                await SyncCourseCatalogFromDocumentsAsync(allDocuments, cancellationToken);
            }
            var allCourseCatalog = await _repository.GetCourseCatalogAsync(cancellationToken);
            var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
            var courseCatalog = FilterCourseCatalogForCurrentUser(allCourseCatalog).ToList();
            var accessibleDocuments = FilterDocumentsForCurrentUser(allDocuments, allCourseCatalog, currentUser).ToList();
            var normalizedSubjectFilter = subjectFilter?.Trim();
            var documents = string.IsNullOrWhiteSpace(normalizedSubjectFilter)
                ? accessibleDocuments
                : accessibleDocuments
                    .Where(document => SubjectMatchesFilter(document.Subject, normalizedSubjectFilter))
                    .ToList();
            var lecturers = IsAdmin()
                ? await _users.GetByRoleAsync(AppRoles.Lecturer, cancellationToken)
                : Array.Empty<UserAccount>();
            var indexedDocuments = accessibleDocuments
                .Where(document => document.Status == DocumentIndexStatus.Indexed)
                .ToList();

            var model = new HomeIndexViewModel
            {
                Documents = documents,
                CourseCatalog = courseCatalog,
                LecturerOptions = lecturers.Select(ToUserOption).ToList(),
                DocumentSubjectOptions = accessibleDocuments
                    .Select(document => document.Subject)
                    .Where(subject => !string.IsNullOrWhiteSpace(subject))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(subject => subject)
                    .ToList(),
                DocumentChapterOptions = accessibleDocuments
                    .Select(document => document.Chapter)
                    .Where(chapter => !string.IsNullOrWhiteSpace(chapter))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(chapter => chapter)
                    .ToList(),
                SubjectFilter = normalizedSubjectFilter,
                IsAdmin = IsAdmin(),
                IsLecturer = IsLecturer(),
                TotalDocumentCount = accessibleDocuments.Count,
                TotalChunkCount = accessibleDocuments.Sum(document => document.ChunkCount),
                TotalUploadedBytes = accessibleDocuments.Sum(document => document.FileSizeBytes),
                IndexedDocumentCount = indexedDocuments.Count,
                ProcessingDocumentCount = accessibleDocuments.Count(document => document.Status == DocumentIndexStatus.Processing),
                FailedDocumentCount = accessibleDocuments.Count(document => document.Status == DocumentIndexStatus.Failed),
                AverageChunksPerIndexedDocument = indexedDocuments.Count == 0
                    ? 0
                    : indexedDocuments.Average(document => document.ChunkCount)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> SaveSubject(SubjectCatalogViewModel model, CancellationToken cancellationToken)
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            try
            {
                var ownerInfo = await BuildSubjectOwnerInfoAsync(model.OwnerUserId, cancellationToken);
                await _repository.UpsertSubjectAsync(model.Id, model.Code, model.Name, model.Description, cancellationToken, ownerInfo);
                TempData["Success"] = "Đã lưu môn học.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ToVietnameseCatalogError(ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> DeleteSubject(Guid id, CancellationToken cancellationToken)
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            await _repository.DeleteSubjectAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa môn học.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> SaveChapter(ChapterCatalogViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                if (!await CanManageSubjectAsync(model.SubjectId, cancellationToken))
                {
                    return Forbid();
                }

                await _repository.UpsertChapterAsync(model.Id, model.SubjectId, model.Title, model.SortOrder, cancellationToken);
                TempData["Success"] = "Đã lưu chương.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ToVietnameseCatalogError(ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> DeleteChapter(Guid id, CancellationToken cancellationToken)
        {
            if (!await CanManageChapterAsync(id, cancellationToken))
            {
                return Forbid();
            }

            await _repository.DeleteChapterAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa chương.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> Chat(CancellationToken cancellationToken)
        {
            var documents = await _indexingService.GetDocumentsAsync(cancellationToken);
            var courseCatalog = await _repository.GetCourseCatalogAsync(cancellationToken);
            var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
            var allIndexedDocuments = documents
                .Where(document => document.Status == DocumentIndexStatus.Indexed)
                .ToList();
            var indexedDocuments = FilterDocumentsForCurrentUser(allIndexedDocuments, courseCatalog, currentUser).ToList();
            var chatSessions = currentUser is null
                ? Array.Empty<ChatSession>()
                : await _repository.GetSessionsForOwnerAsync(currentUser.Id, cancellationToken);
            var model = new ChatIndexViewModel
            {
                ChatSessions = chatSessions,
                Documents = indexedDocuments,
                SubjectOptions = indexedDocuments
                    .Select(document => document.Subject)
                    .Where(subject => !string.IsNullOrWhiteSpace(subject))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(subject => subject)
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> Upload(DocumentUploadViewModel model, CancellationToken cancellationToken)
        {
            var isVietnamese = model.Language?.Equals("vi", StringComparison.OrdinalIgnoreCase) == true;
            if ((model.File is null || model.File.Length == 0) && string.IsNullOrWhiteSpace(model.SourceUrl))
            {
                TempData["Error"] = isVietnamese
                    ? "Hãy chọn file PDF, DOCX, PPTX, TXT hoặc nhập URL trang bài giảng trước khi index."
                    : "Choose a PDF, DOCX, PPTX, TXT file or enter a web page URL before indexing.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (string.IsNullOrWhiteSpace(model.Subject) || string.IsNullOrWhiteSpace(model.Chapter))
                {
                    TempData["Error"] = isVietnamese
                        ? "Subject va Chapter la bat buoc khi upload tai lieu."
                        : "Subject and Chapter are required when uploading a document.";
                    return RedirectToAction(nameof(Index));
                }

                if (!await CanManageSubjectAsync(model.Subject, cancellationToken))
                {
                    return Forbid();
                }

                DocumentUploadResult result;
                var uploader = BuildDocumentUploaderInfo();
                if (model.File is { Length: > 0 })
                {
                    await using var stream = model.File.OpenReadStream();
                    result = await _indexingService.QueueFileAsync(
                        stream,
                        model.File.FileName,
                        model.File.ContentType,
                        model.Subject,
                        model.Chapter,
                        Path.Combine(_environment.ContentRootPath, "App_Data", "uploads"),
                        uploader,
                        cancellationToken);
                }
                else
                {
                    var extracted = await _webPageTextExtractor.ExtractAsync(model.SourceUrl ?? string.Empty, cancellationToken);
                    var sourceName = $"{extracted.Title} - {new Uri(extracted.SourceUrl).Host}.txt";
                    result = await _indexingService.QueueTextAsync(
                        extracted.Text,
                        sourceName,
                        extracted.UsedBrowserRenderer ? "text/html+playwright" : "text/html",
                        model.Subject,
                        model.Chapter,
                        Path.Combine(_environment.ContentRootPath, "App_Data", "uploads"),
                        uploader,
                        cancellationToken);
                }

                await _indexJobQueue.EnqueueAsync(result.DocumentId, cancellationToken);

                TempData["Success"] = isVietnamese
                    ? "Da nhan tai lieu va dang index."
                    : "The document has been queued for indexing.";

                var indexedDocument = await _repository.GetDocumentAsync(result.DocumentId, cancellationToken);
                if (indexedDocument is not null)
                {
                    await SyncCourseCatalogFromDocumentsAsync(new[] { indexedDocument }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document upload failed");
                TempData["Error"] = isVietnamese ? ToVietnameseUploadError(ex.Message) : ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> Ask([FromBody] ChatRequest? request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { error = "Invalid question payload." });
            }

            if (!Guid.TryParse(request.SessionId, out var sessionId))
            {
                sessionId = Guid.NewGuid();
            }

            try
            {
                var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
                var courseCatalog = await _repository.GetCourseCatalogAsync(cancellationToken);
                var indexedDocuments = (await _indexingService.GetDocumentsAsync(cancellationToken))
                    .Where(document => document.Status == DocumentIndexStatus.Indexed)
                    .ToList();
                var accessibleDocuments = FilterDocumentsForCurrentUser(indexedDocuments, courseCatalog, currentUser).ToList();
                var allowedSubjects = accessibleDocuments
                    .Select(document => document.Subject)
                    .Where(subject => !string.IsNullOrWhiteSpace(subject))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var displayName = User.FindFirstValue(ClaimTypes.Name)
                    ?? User.FindFirstValue(ClaimTypes.Email)?.Split('@')[0];
                if (currentUser is not null)
                {
                    var existingSession = await _repository.GetSessionAsync(sessionId, cancellationToken);
                    if (existingSession?.OwnerUserId is { } ownerUserId && ownerUserId != currentUser.Id)
                    {
                        sessionId = Guid.NewGuid();
                    }
                }

                var answer = await _chatService.AskAsync(
                    sessionId,
                    request.Question ?? string.Empty,
                    displayName,
                    request.SubjectFilter,
                    request.Language,
                    allowedSubjects,
                    BuildChatSessionOwnerInfo(),
                    cancellationToken);
                return Json(new
                {
                    sessionId,
                    answer = answer.Answer,
                    citations = answer.Citations,
                    resolvedSubject = answer.ResolvedSubject,
                    needsClarification = answer.NeedsClarification,
                    subjectOptions = answer.SubjectOptions,
                    answerSource = answer.AnswerSource,
                    hasDirectCitation = answer.HasDirectCitation,
                    fallbackModel = answer.FallbackModel
                });
            }
            catch (Exception ex)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> CreateChatSession(CancellationToken cancellationToken)
        {
            var session = await _repository.GetOrCreateSessionAsync(Guid.NewGuid(), cancellationToken, BuildChatSessionOwnerInfo());
            return Json(ToSessionSummary(session));
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> RenameChatSession([FromBody] ChatSessionRenameRequest? request, CancellationToken cancellationToken)
        {
            if (request is null || !Guid.TryParse(request.SessionId, out var sessionId))
            {
                return BadRequest(new { error = "Invalid chat session." });
            }

            try
            {
                var session = await _repository.RenameSessionAsync(
                    sessionId,
                    request.Title ?? string.Empty,
                    cancellationToken,
                    BuildChatSessionOwnerInfo());
                return session is null
                    ? NotFound(new { error = "Chat session not found." })
                    : Json(ToSessionSummary(session));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> StarChatSession([FromBody] ChatSessionStarRequest? request, CancellationToken cancellationToken)
        {
            if (request is null || !Guid.TryParse(request.SessionId, out var sessionId))
            {
                return BadRequest(new { error = "Invalid chat session." });
            }

            try
            {
                var session = await _repository.SetSessionStarredAsync(
                    sessionId,
                    request.IsStarred,
                    cancellationToken,
                    BuildChatSessionOwnerInfo());
                return session is null
                    ? NotFound(new { error = "Chat session not found." })
                    : Json(ToSessionSummary(session));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> DeleteChatSession([FromBody] ChatSessionDeleteRequest? request, CancellationToken cancellationToken)
        {
            if (request is null || !Guid.TryParse(request.SessionId, out var sessionId))
            {
                return BadRequest(new { error = "Invalid chat session." });
            }

            try
            {
                var deleted = await _repository.DeleteSessionAsync(sessionId, cancellationToken, BuildChatSessionOwnerInfo());
                return deleted
                    ? Json(new { deleted = true, sessionId })
                    : NotFound(new { error = "Chat session not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> EditDocument(Guid id, CancellationToken cancellationToken)
        {
            var document = await _repository.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound();
            }

            if (!await CanManageDocumentAsync(document, cancellationToken))
            {
                return Forbid();
            }

            return View(new DocumentEditViewModel
            {
                Id = document.Id,
                FileName = document.FileName,
                Subject = document.Subject,
                Chapter = document.Chapter,
                ContentType = document.ContentType,
                UploadedAt = document.UploadedAt,
                ChunkCount = document.ChunkCount,
                FileSizeBytes = document.FileSizeBytes,
                UploadedByName = document.UploadedByName,
                UploadedByEmail = document.UploadedByEmail,
                CourseCatalog = FilterCourseCatalogForCurrentUser(await _repository.GetCourseCatalogAsync(cancellationToken)).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> EditDocument(DocumentEditViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                var existing = await _repository.GetDocumentAsync(model.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Document not found.");
                if (!await CanManageDocumentAsync(existing, cancellationToken)
                    || !await CanManageSubjectAsync(model.Subject, cancellationToken))
                {
                    return Forbid();
                }

                var document = await _repository.UpdateDocumentMetadataAsync(
                    model.Id,
                    model.FileName,
                    model.Subject,
                    model.Chapter,
                    cancellationToken);
                await SyncCourseCatalogFromDocumentsAsync(new[] { document }, cancellationToken);
                TempData["Success"] = $"Đã cập nhật tài liệu {document.FileName}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ToVietnameseDocumentError(ex.Message);
                var existing = await _repository.GetDocumentAsync(model.Id, cancellationToken);
                if (existing is not null)
                {
                    model.ContentType = existing.ContentType;
                    model.UploadedAt = existing.UploadedAt;
                    model.ChunkCount = existing.ChunkCount;
                    model.FileSizeBytes = existing.FileSizeBytes;
                    model.UploadedByName = existing.UploadedByName;
                    model.UploadedByEmail = existing.UploadedByEmail;
                }

                model.CourseCatalog = FilterCourseCatalogForCurrentUser(await _repository.GetCourseCatalogAsync(cancellationToken)).ToList();
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
        {
            var document = await _repository.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                TempData["Error"] = "Không tìm thấy tài liệu để xóa.";
                return RedirectToAction(nameof(Index));
            }

            if (!await CanManageDocumentAsync(document, cancellationToken))
            {
                return Forbid();
            }

            await _repository.DeleteDocumentAsync(id, cancellationToken);
            TryDeleteStoredFile(document);
            TempData["Success"] = $"Đã xóa tài liệu {document.FileName}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.DocumentRead)]
        public async Task<IActionResult> DocumentPreview(Guid id, CancellationToken cancellationToken)
        {
            var document = await _repository.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound(new { error = "Document not found." });
            }

            if (!await CanViewDocumentAsync(document, cancellationToken))
            {
                return Forbid();
            }

            if (document.Status != DocumentIndexStatus.Indexed)
            {
                return BadRequest(new
                {
                    error = string.IsNullOrWhiteSpace(document.IndexError)
                        ? "Document is not indexed yet."
                        : document.IndexError,
                    status = document.Status
                });
            }

            var subjectOwner = await ResolveSubjectOwnerAsync(document.Subject, cancellationToken);
            var chunks = await _repository.GetDocumentChunksAsync(document.Id, cancellationToken);
            var preview = new DocumentPreviewViewModel
            {
                Id = document.Id,
                FileName = document.FileName,
                Subject = document.Subject,
                Chapter = document.Chapter,
                ContentType = document.ContentType,
                UploadedAt = document.UploadedAt,
                ChunkCount = document.ChunkCount,
                FileSizeBytes = document.FileSizeBytes,
                UploadedByName = document.UploadedByName,
                UploadedByEmail = document.UploadedByEmail,
                Status = document.Status,
                IndexedAt = document.IndexedAt,
                IndexError = document.IndexError,
                EmbeddingModel = document.EmbeddingModel,
                EmbeddingDimensions = document.EmbeddingDimensions,
                ChunkingStrategy = document.ChunkingStrategy,
                SubjectOwnerName = subjectOwner.Name,
                SubjectOwnerEmail = subjectOwner.Email,
                Chunks = chunks
                    .Select(chunk => new DocumentPreviewChunkViewModel
                    {
                        ChunkIndex = chunk.ChunkIndex,
                        SectionTitle = chunk.SectionTitle,
                        CharStart = chunk.CharStart,
                        CharEnd = chunk.CharEnd,
                        Text = chunk.Text
                    })
                    .ToList()
            };

            return Json(preview);
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.DocumentRead)]
        public async Task<IActionResult> ViewDocument(Guid id, CancellationToken cancellationToken)
        {
            var document = await _repository.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound();
            }

            if (!await CanViewDocumentAsync(document, cancellationToken))
            {
                return Forbid();
            }

            var storedPath = Path.GetFullPath(document.StoredPath);
            if (!IsPathUnderDirectory(storedPath, GetUploadsRoot()) || !System.IO.File.Exists(storedPath))
            {
                return NotFound();
            }

            if (IsTextDocument(document))
            {
                var content = await System.IO.File.ReadAllTextAsync(storedPath, Encoding.UTF8, cancellationToken);
                return View("DocumentText", new DocumentTextViewModel
                {
                    Document = document,
                    Content = content
                });
            }

            Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName.Replace("\"", string.Empty)}\"";
            return PhysicalFile(storedPath, ResolveContentType(document), enableRangeProcessing: true);
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> ChatSessions(CancellationToken cancellationToken)
        {
            var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
            var sessions = currentUser is null
                ? Array.Empty<ChatSession>()
                : await _repository.GetSessionsForOwnerAsync(currentUser.Id, cancellationToken);
            return Json(sessions.Select(ToSessionSummary));
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> ChatSession(Guid id, CancellationToken cancellationToken)
        {
            var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
            if (currentUser is null)
            {
                return NotFound(new { error = "Chat session not found." });
            }

            var session = await _repository.GetSessionForOwnerAsync(id, currentUser.Id, cancellationToken);
            if (session is null)
            {
                return NotFound(new { error = "Chat session not found." });
            }

            return Json(new
            {
                id = session.Id,
                title = GetSessionTitle(session),
                isStarred = session.IsStarred,
                createdAt = session.CreatedAt,
                updatedAt = session.UpdatedAt,
                messages = session.Messages
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => new
                    {
                        role = message.Role,
                        content = message.Content,
                        createdAt = message.CreatedAt,
                        citations = message.Citations.Select(citation => new
                        {
                            documentId = citation.DocumentId,
                            fileName = citation.FileName,
                            subject = citation.Subject,
                            chapter = citation.Chapter,
                            chunkIndex = citation.ChunkIndex,
                            score = citation.Score,
                            excerpt = citation.Excerpt
                        })
                    })
            });
        }

        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public IActionResult Privacy()
        {
            return View();
        }

        private static object ToSessionSummary(ChatSession session)
        {
            return new
            {
                id = session.Id,
                title = GetSessionTitle(session),
                isStarred = session.IsStarred,
                createdAt = session.CreatedAt,
                updatedAt = session.UpdatedAt,
                messageCount = session.Messages.Count
            };
        }

        private string CurrentRole()
        {
            return AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role));
        }

        private bool IsAdmin()
        {
            return CurrentRole() == AppRoles.Admin;
        }

        private bool IsLecturer()
        {
            return CurrentRole() == AppRoles.Lecturer;
        }

        private bool CanManageDocuments()
        {
            return IsAdmin() || IsLecturer();
        }

        private Guid? CurrentUserId()
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;
        }

        private static UserOptionViewModel ToUserOption(UserAccount user)
        {
            return new UserOptionViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email
            };
        }

        private async Task<SubjectOwnerInfo> BuildSubjectOwnerInfoAsync(Guid? ownerUserId, CancellationToken cancellationToken)
        {
            if (!ownerUserId.HasValue)
            {
                return new SubjectOwnerInfo(null, string.Empty, string.Empty);
            }

            var lecturer = (await _users.GetByRoleAsync(AppRoles.Lecturer, cancellationToken))
                .FirstOrDefault(user => user.Id == ownerUserId.Value);
            if (lecturer is null)
            {
                throw new InvalidOperationException("Lecturer owner not found.");
            }

            return new SubjectOwnerInfo(lecturer.Id, lecturer.FullName, lecturer.Email);
        }

        private DocumentUploaderInfo BuildDocumentUploaderInfo()
        {
            return new DocumentUploaderInfo(
                CurrentUserId(),
                User.FindFirstValue(ClaimTypes.Name),
                User.FindFirstValue(ClaimTypes.Email));
        }

        private ChatSessionOwnerInfo BuildChatSessionOwnerInfo()
        {
            return new ChatSessionOwnerInfo(
                CurrentUserId(),
                User.FindFirstValue(ClaimTypes.Name),
                User.FindFirstValue(ClaimTypes.Email));
        }

        private async Task<UserAccount?> GetCurrentUserAccountAsync(CancellationToken cancellationToken)
        {
            if (CurrentUserId() is not { } userId)
            {
                return null;
            }

            return (await _users.GetAllAsync(cancellationToken))
                .FirstOrDefault(user => user.Id == userId);
        }

        private IReadOnlyList<CourseSubject> FilterCourseCatalogForCurrentUser(IReadOnlyList<CourseSubject> catalog)
        {
            if (IsAdmin())
            {
                return catalog;
            }

            if (!IsLecturer() || CurrentUserId() is not { } userId)
            {
                return Array.Empty<CourseSubject>();
            }

            return catalog
                .Where(subject => subject.OwnerUserId == userId)
                .ToList();
        }

        private IReadOnlyList<IndexedDocument> FilterDocumentsForCurrentUser(
            IReadOnlyList<IndexedDocument> documents,
            IReadOnlyList<CourseSubject> catalog,
            UserAccount? currentUser)
        {
            if (IsAdmin())
            {
                return documents;
            }

            if (currentUser is null)
            {
                return Array.Empty<IndexedDocument>();
            }

            if (IsLecturer())
            {
                return documents
                    .Where(document => FindSubjectForDocumentSubject(catalog, document.Subject)?.OwnerUserId == currentUser.Id)
                    .ToList();
            }

            if (CurrentRole() == AppRoles.Student)
            {
                return documents
                    .Where(document => document.Status == DocumentIndexStatus.Indexed)
                    .ToList();
            }

            return Array.Empty<IndexedDocument>();
        }

        private async Task<bool> CanManageSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
        {
            if (IsAdmin())
            {
                return true;
            }

            if (!IsLecturer() || CurrentUserId() is not { } userId)
            {
                return false;
            }

            var subject = (await _repository.GetCourseCatalogAsync(cancellationToken))
                .FirstOrDefault(item => item.Id == subjectId);
            return subject?.OwnerUserId == userId;
        }

        private async Task<bool> CanManageSubjectAsync(string subjectText, CancellationToken cancellationToken)
        {
            if (IsAdmin())
            {
                return true;
            }

            if (!IsLecturer() || CurrentUserId() is not { } userId)
            {
                return false;
            }

            var subject = FindSubjectForDocumentSubject(await _repository.GetCourseCatalogAsync(cancellationToken), subjectText);
            return subject?.OwnerUserId == userId;
        }

        private async Task<bool> CanManageDocumentAsync(IndexedDocument document, CancellationToken cancellationToken)
        {
            return await CanManageSubjectAsync(document.Subject, cancellationToken);
        }

        private async Task<bool> CanViewDocumentAsync(IndexedDocument document, CancellationToken cancellationToken)
        {
            if (CanManageDocuments())
            {
                return await CanManageDocumentAsync(document, cancellationToken);
            }

            return CurrentRole() == AppRoles.Student
                   && document.Status == DocumentIndexStatus.Indexed;
        }

        private async Task<bool> CanManageChapterAsync(Guid chapterId, CancellationToken cancellationToken)
        {
            if (IsAdmin())
            {
                return true;
            }

            if (!IsLecturer() || CurrentUserId() is not { } userId)
            {
                return false;
            }

            var subject = (await _repository.GetCourseCatalogAsync(cancellationToken))
                .FirstOrDefault(item => item.Chapters.Any(chapter => chapter.Id == chapterId));
            return subject?.OwnerUserId == userId;
        }

        private async Task<(string Name, string Email)> ResolveSubjectOwnerAsync(string subjectText, CancellationToken cancellationToken)
        {
            var subject = FindSubjectForDocumentSubject(await _repository.GetCourseCatalogAsync(cancellationToken), subjectText);
            return subject is null
                ? (string.Empty, string.Empty)
                : (subject.OwnerName, subject.OwnerEmail);
        }

        private static CourseSubject? FindSubjectForDocumentSubject(IEnumerable<CourseSubject> catalog, string subjectText)
        {
            var normalizedSubject = (subjectText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedSubject))
            {
                return null;
            }

            var parsed = ParseSubjectForCatalog(normalizedSubject);
            return catalog.FirstOrDefault(subject =>
                subject.DisplayName.Equals(normalizedSubject, StringComparison.OrdinalIgnoreCase)
                || subject.Code.Equals(normalizedSubject, StringComparison.OrdinalIgnoreCase)
                || subject.Name.Equals(normalizedSubject, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(parsed.Code)
                    && subject.Code.Equals(parsed.Code, StringComparison.OrdinalIgnoreCase)));
        }

        private static string ToVietnameseUploadError(string message)
        {
            if (message.Contains("Only PDF, DOCX, PPTX, and TXT files", StringComparison.OrdinalIgnoreCase))
            {
                return "Chỉ hỗ trợ file PDF, DOCX, PPTX và TXT.";
            }

            if (message.Contains("selected file is empty", StringComparison.OrdinalIgnoreCase))
            {
                return "File đã chọn đang trống nên không thể index.";
            }

            if (message.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                return "Tài liệu này đã tồn tại trong kho.";
            }

            return string.IsNullOrWhiteSpace(message) ? "Không thể xử lý tài liệu." : message;
        }

        private async Task SyncCourseCatalogFromDocumentsAsync(
            IReadOnlyList<IndexedDocument> documents,
            CancellationToken cancellationToken)
        {
            foreach (var document in documents.Where(item => !string.IsNullOrWhiteSpace(item.Subject)))
            {
                try
                {
                    await SyncCourseCatalogFromDocumentAsync(document, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not sync course catalog from document {DocumentId}", document.Id);
                }
            }
        }

        private async Task SyncCourseCatalogFromDocumentAsync(IndexedDocument document, CancellationToken cancellationToken)
        {
            var parsed = ParseSubjectForCatalog(document.Subject);
            if (string.IsNullOrWhiteSpace(parsed.Code))
            {
                return;
            }

            var catalog = await _repository.GetCourseCatalogAsync(cancellationToken);
            var subject = catalog.FirstOrDefault(item =>
                item.Code.Equals(parsed.Code, StringComparison.OrdinalIgnoreCase)
                || item.DisplayName.Equals(document.Subject.Trim(), StringComparison.OrdinalIgnoreCase));

            if (subject is null)
            {
                subject = await _repository.UpsertSubjectAsync(
                    subjectId: null,
                    code: parsed.Code,
                    name: parsed.Name,
                    description: "Tự đồng bộ từ tài liệu đã index.",
                    cancellationToken);
            }
            else if (string.IsNullOrWhiteSpace(subject.Name)
                     || subject.Name.Equals(subject.Code, StringComparison.OrdinalIgnoreCase))
            {
                subject = await _repository.UpsertSubjectAsync(
                    subject.Id,
                    subject.Code,
                    parsed.Name,
                    subject.Description,
                    cancellationToken);
            }

            var chapterTitle = document.Chapter.Trim();
            if (string.IsNullOrWhiteSpace(chapterTitle)
                || subject.Chapters.Any(item => item.Title.Equals(chapterTitle, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var nextSortOrder = subject.Chapters.Count == 0
                ? 1
                : subject.Chapters.Max(item => item.SortOrder) + 1;
            await _repository.UpsertChapterAsync(
                chapterId: null,
                subject.Id,
                chapterTitle,
                nextSortOrder,
                cancellationToken);
        }

        private static (string Code, string Name) ParseSubjectForCatalog(string subject)
        {
            var trimmed = subject.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return (string.Empty, string.Empty);
            }

            var separatorIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
            var separatorLength = 3;
            if (separatorIndex < 0)
            {
                separatorIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
                separatorLength = 1;
            }

            if (separatorIndex > 0)
            {
                var codeCandidate = NormalizeCatalogCode(trimmed[..separatorIndex]);
                var nameCandidate = trimmed[(separatorIndex + separatorLength)..].Trim();
                if (!string.IsNullOrWhiteSpace(codeCandidate) && !string.IsNullOrWhiteSpace(nameCandidate))
                {
                    return (codeCandidate, nameCandidate);
                }
            }

            var firstToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? trimmed;
            var code = NormalizeCatalogCode(firstToken);
            return string.IsNullOrWhiteSpace(code)
                ? (string.Empty, string.Empty)
                : (code, trimmed);
        }

        private static bool SubjectMatchesFilter(string documentSubject, string subjectFilter)
        {
            var normalizedDocumentSubject = (documentSubject ?? string.Empty).Trim();
            var normalizedSubjectFilter = (subjectFilter ?? string.Empty).Trim();
            if (normalizedDocumentSubject.Equals(normalizedSubjectFilter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var documentCode = ParseSubjectForCatalog(normalizedDocumentSubject).Code;
            var filterCode = ParseSubjectForCatalog(normalizedSubjectFilter).Code;
            return !string.IsNullOrWhiteSpace(documentCode)
                && documentCode.Equals(filterCode, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCatalogCode(string code)
        {
            return new string((code ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Where(character => char.IsLetterOrDigit(character) || character is '_' or '.')
                .Take(32)
                .ToArray());
        }

        private static string ToVietnameseCatalogError(string message)
        {
            if (message.Contains("Subject code is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Mã môn học là bắt buộc.";
            }

            if (message.Contains("Subject code already exists", StringComparison.OrdinalIgnoreCase))
            {
                return "Mã môn học đã tồn tại.";
            }

            if (message.Contains("Lecturer owner not found", StringComparison.OrdinalIgnoreCase))
            {
                return "Không tìm thấy giảng viên phụ trách hợp lệ.";
            }

            if (message.Contains("Chapter title is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Tên chương là bắt buộc.";
            }

            if (message.Contains("Chapter already exists", StringComparison.OrdinalIgnoreCase))
            {
                return "Chương này đã tồn tại trong môn học.";
            }

            return string.IsNullOrWhiteSpace(message) ? "Không thể lưu danh mục môn/chương." : message;
        }

        private static string ToVietnameseDocumentError(string message)
        {
            if (message.Contains("Document not found", StringComparison.OrdinalIgnoreCase))
            {
                return "Không tìm thấy tài liệu.";
            }

            if (message.Contains("File name is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Tên file là bắt buộc.";
            }

            if (message.Contains("Subject is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Subject là bắt buộc.";
            }

            if (message.Contains("Chapter is required", StringComparison.OrdinalIgnoreCase))
            {
                return "Chapter là bắt buộc.";
            }

            return string.IsNullOrWhiteSpace(message) ? "Không thể cập nhật tài liệu." : message;
        }

        private string GetUploadsRoot()
        {
            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "uploads"));
        }

        private void TryDeleteStoredFile(IndexedDocument document)
        {
            try
            {
                var storedPath = Path.GetFullPath(document.StoredPath);
                if (IsPathUnderDirectory(storedPath, GetUploadsRoot()) && System.IO.File.Exists(storedPath))
                {
                    System.IO.File.Delete(storedPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete stored file for document {DocumentId}", document.Id);
            }
        }

        private static bool IsPathUnderDirectory(string path, string directory)
        {
            var fullPath = Path.GetFullPath(path);
            var fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
            {
                fullDirectory += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveContentType(IndexedDocument document)
        {
            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
            if (extension == ".txt")
            {
                return "text/plain; charset=utf-8";
            }

            if (!string.IsNullOrWhiteSpace(document.ContentType) && document.ContentType != "application/octet-stream")
            {
                if (document.ContentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase)
                    || document.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    return "text/plain; charset=utf-8";
                }

                return document.ContentType;
            }

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".txt" => "text/plain; charset=utf-8",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/octet-stream"
            };
        }

        private static bool IsTextDocument(IndexedDocument document)
        {
            var extension = Path.GetExtension(document.FileName);
            return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                   || document.ContentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase)
                   || document.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSessionTitle(ChatSession session)
        {
            if (!string.IsNullOrWhiteSpace(session.Title))
            {
                return session.Title.Trim();
            }

            var firstQuestion = session.Messages
                .FirstOrDefault(message => message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                ?.Content
                .Trim();

            if (string.IsNullOrWhiteSpace(firstQuestion))
            {
                return "Phiên chưa có câu hỏi";
            }

            return firstQuestion.Length <= 56 ? firstQuestion : $"{firstQuestion[..56]}...";
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
