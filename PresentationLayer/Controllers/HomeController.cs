using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using DataAccessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Models;
using PresentationLayer.Security;
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
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            ILogger<HomeController> logger,
            IKnowledgeRepository repository,
            IDocumentIndexingService indexingService,
            IWebPageTextExtractor webPageTextExtractor,
            IRagChatService chatService,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _repository = repository;
            _indexingService = indexingService;
            _webPageTextExtractor = webPageTextExtractor;
            _chatService = chatService;
            _environment = environment;
        }

        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> Index(string? subjectFilter, CancellationToken cancellationToken)
        {
            var allDocuments = await _indexingService.GetDocumentsAsync(cancellationToken);
            await SyncCourseCatalogFromDocumentsAsync(allDocuments, cancellationToken);
            var normalizedSubjectFilter = subjectFilter?.Trim();
            var documents = string.IsNullOrWhiteSpace(normalizedSubjectFilter)
                ? allDocuments
                : allDocuments
                    .Where(document => SubjectMatchesFilter(document.Subject, normalizedSubjectFilter))
                    .ToList();

            var model = new HomeIndexViewModel
            {
                Documents = documents,
                CourseCatalog = await _repository.GetCourseCatalogAsync(cancellationToken),
                DocumentSubjectOptions = allDocuments
                    .Select(document => document.Subject)
                    .Where(subject => !string.IsNullOrWhiteSpace(subject))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(subject => subject)
                    .ToList(),
                DocumentChapterOptions = allDocuments
                    .Select(document => document.Chapter)
                    .Where(chapter => !string.IsNullOrWhiteSpace(chapter))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(chapter => chapter)
                    .ToList(),
                SubjectFilter = normalizedSubjectFilter,
                TotalDocumentCount = allDocuments.Count,
                TotalChunkCount = allDocuments.Sum(document => document.ChunkCount),
                TotalUploadedBytes = allDocuments.Sum(document => document.FileSizeBytes)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> SaveSubject(SubjectCatalogViewModel model, CancellationToken cancellationToken)
        {
            try
            {
                await _repository.UpsertSubjectAsync(model.Id, model.Code, model.Name, model.Description, cancellationToken);
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
            await _repository.DeleteChapterAsync(id, cancellationToken);
            TempData["Success"] = "Đã xóa chương.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> Chat(CancellationToken cancellationToken)
        {
            var model = new ChatIndexViewModel
            {
                ChatSessions = await _repository.GetSessionsAsync(cancellationToken),
                Documents = await _indexingService.GetDocumentsAsync(cancellationToken)
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
                DocumentUploadResult result;
                if (model.File is { Length: > 0 })
                {
                    await using var stream = model.File.OpenReadStream();
                    result = await _indexingService.IndexAsync(
                        stream,
                        model.File.FileName,
                        model.File.ContentType,
                        model.Subject,
                        model.Chapter,
                        Path.Combine(_environment.ContentRootPath, "App_Data", "uploads"),
                        cancellationToken);
                }
                else
                {
                    var extracted = await _webPageTextExtractor.ExtractAsync(model.SourceUrl ?? string.Empty, cancellationToken);
                    var sourceName = $"{extracted.Title} - {new Uri(extracted.SourceUrl).Host}.txt";
                    result = await _indexingService.IndexTextAsync(
                        extracted.Text,
                        sourceName,
                        extracted.UsedBrowserRenderer ? "text/html+playwright" : "text/html",
                        model.Subject,
                        model.Chapter,
                        Path.Combine(_environment.ContentRootPath, "App_Data", "uploads"),
                        cancellationToken);
                }

                TempData["Success"] = isVietnamese
                    ? "Đã index tài liệu."
                    : "The document has been indexed.";

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
                var displayName = User.FindFirstValue(ClaimTypes.Name)
                    ?? User.FindFirstValue(ClaimTypes.Email)?.Split('@')[0];
                var answer = await _chatService.AskAsync(
                    sessionId,
                    request.Question ?? string.Empty,
                    displayName,
                    request.Language,
                    cancellationToken);
                return Json(new
                {
                    sessionId,
                    answer = answer.Answer,
                    citations = answer.Citations
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
            var session = await _repository.GetOrCreateSessionAsync(Guid.NewGuid(), cancellationToken);
            return Json(ToSessionSummary(session));
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
                CourseCatalog = await _repository.GetCourseCatalogAsync(cancellationToken)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> EditDocument(DocumentEditViewModel model, CancellationToken cancellationToken)
        {
            try
            {
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
                }

                model.CourseCatalog = await _repository.GetCourseCatalogAsync(cancellationToken);
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

            await _repository.DeleteDocumentAsync(id, cancellationToken);
            TryDeleteStoredFile(document);
            TempData["Success"] = $"Đã xóa tài liệu {document.FileName}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.DocumentManagement)]
        public async Task<IActionResult> ViewDocument(Guid id, CancellationToken cancellationToken)
        {
            var document = await _repository.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound();
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
            var sessions = await _repository.GetSessionsAsync(cancellationToken);
            return Json(sessions.Select(ToSessionSummary));
        }

        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.ChatAccess)]
        public async Task<IActionResult> ChatSession(Guid id, CancellationToken cancellationToken)
        {
            var session = await _repository.GetSessionAsync(id, cancellationToken);
            if (session is null)
            {
                return NotFound(new { error = "Chat session not found." });
            }

            return Json(new
            {
                id = session.Id,
                title = GetSessionTitle(session),
                createdAt = session.CreatedAt,
                updatedAt = session.UpdatedAt,
                messages = session.Messages
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => new
                    {
                        role = message.Role,
                        content = message.Content,
                        createdAt = message.CreatedAt
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
                createdAt = session.CreatedAt,
                updatedAt = session.UpdatedAt,
                messageCount = session.Messages.Count
            };
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
