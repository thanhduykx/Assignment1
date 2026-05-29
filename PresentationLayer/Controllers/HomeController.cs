using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using DataAccessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Models;
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

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var model = new HomeIndexViewModel
            {
                Documents = await _indexingService.GetDocumentsAsync(cancellationToken)
            };

            return View(model);
        }

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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document upload failed");
                TempData["Error"] = isVietnamese ? ToVietnameseUploadError(ex.Message) : ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
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
        public async Task<IActionResult> CreateChatSession(CancellationToken cancellationToken)
        {
            var session = await _repository.GetOrCreateSessionAsync(Guid.NewGuid(), cancellationToken);
            return Json(ToSessionSummary(session));
        }

        [HttpGet]
        public async Task<IActionResult> ViewDocument(Guid id, CancellationToken cancellationToken)
        {
            var document = await _repository.GetDocumentAsync(id, cancellationToken);
            if (document is null)
            {
                return NotFound();
            }

            var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "uploads"));
            var storedPath = Path.GetFullPath(document.StoredPath);
            if (!storedPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(storedPath))
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
        public async Task<IActionResult> ChatSessions(CancellationToken cancellationToken)
        {
            var sessions = await _repository.GetSessionsAsync(cancellationToken);
            return Json(sessions.Select(ToSessionSummary));
        }

        [HttpGet]
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
