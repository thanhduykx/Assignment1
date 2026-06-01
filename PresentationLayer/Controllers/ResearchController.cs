using System.Collections.Concurrent;
using DataAccessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PresentationLayer.Models;
using PresentationLayer.Services;
using ServicesLayer;

namespace PresentationLayer.Controllers;

[Authorize]
public sealed class ResearchController : Controller
{
    private static readonly ConcurrentDictionary<Guid, byte> RunningBenchmarks = new();

    private readonly IResearchBenchmarkService _researchService;
    private readonly IResearchReportPdfService _reportPdfService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResearchController> _logger;

    public ResearchController(
        IResearchBenchmarkService researchService,
        IResearchReportPdfService reportPdfService,
        IServiceScopeFactory scopeFactory,
        ILogger<ResearchController> logger)
    {
        _researchService = researchService;
        _reportPdfService = reportPdfService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new ResearchIndexViewModel
        {
            Experiments = await _researchService.GetExperimentsAsync(cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await BuildCreateViewModelAsync(new ResearchCreateViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResearchCreateViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Experiment name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            ModelState.AddModelError(nameof(model.Subject), "Subject is required.");
        }

        var questions = ParseQuestions(model.QuestionsText);
        if (questions.Count == 0)
        {
            ModelState.AddModelError(nameof(model.QuestionsText), "Add at least one question in the format: question | expected answer.");
        }

        if (model.EmbeddingModelIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.EmbeddingModelIds), "Choose Gemini embedding model before creating an experiment.");
        }

        if (model.ChunkingStrategyIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ChunkingStrategyIds), "Choose at least one chunking strategy.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildCreateViewModelAsync(model, cancellationToken));
        }

        try
        {
            var experimentId = await _researchService.CreateExperimentAsync(new CreateResearchExperimentRequest
            {
                Name = model.Name,
                Subject = model.Subject,
                EmbeddingModelIds = model.EmbeddingModelIds,
                ChunkingStrategyIds = model.ChunkingStrategyIds,
                Questions = questions,
                FineTunedModelName = model.FineTunedModelName,
                FineTunedEndpoint = model.FineTunedEndpoint
            }, cancellationToken);

            return RedirectToAction(nameof(Details), new { id = experimentId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create research experiment");
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildCreateViewModelAsync(model, cancellationToken));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var experiment = await _researchService.GetExperimentAsync(id, cancellationToken);
        return experiment is null ? NotFound() : View(new ResearchDetailViewModel { Experiment = experiment });
    }

    [HttpGet]
    public async Task<IActionResult> ReportPdf(Guid id, CancellationToken cancellationToken)
    {
        var experiment = await _researchService.GetExperimentAsync(id, cancellationToken);
        if (experiment is null)
        {
            return NotFound();
        }

        var bytes = _reportPdfService.BuildReport(experiment);
        var fileName = $"rbl-report-{SanitizeFileName(experiment.Name)}-{DateTime.UtcNow:yyyyMMddHHmm}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var experiment = await _researchService.GetExperimentAsync(id, cancellationToken);
        if (experiment is null)
        {
            return NotFound();
        }

        if (!RunningBenchmarks.TryAdd(id, 0))
        {
            TempData["Success"] = "Benchmark is already running. You can leave this page and come back later.";
            return RedirectToAction(nameof(Details), new { id });
        }

        QueueBenchmark(id);
        TempData["Success"] = "Benchmark started in background. You can leave this page and continue working.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private void QueueBenchmark(Guid experimentId)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IResearchBenchmarkService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ResearchController>>();

            try
            {
                await service.RunExperimentAsync(experimentId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background research benchmark failed for experiment {ExperimentId}", experimentId);
            }
            finally
            {
                RunningBenchmarks.TryRemove(experimentId, out _);
            }
        });
    }

    private async Task<ResearchCreateViewModel> BuildCreateViewModelAsync(ResearchCreateViewModel model, CancellationToken cancellationToken)
    {
        var catalog = await _researchService.GetCatalogAsync(cancellationToken);
        if (model.EmbeddingModelIds.Count == 0)
        {
            model.EmbeddingModelIds = catalog.EmbeddingModels.Select(item => item.Id).ToList();
        }

        if (model.ChunkingStrategyIds.Count == 0)
        {
            model.ChunkingStrategyIds = catalog.ChunkingStrategies.Select(item => item.Id).ToList();
        }

        model.EmbeddingModels = catalog.EmbeddingModels.Select(item => new SelectListItem
        {
            Value = item.Id.ToString(),
            Text = $"{item.Name} ({item.Provider})",
            Selected = model.EmbeddingModelIds.Contains(item.Id)
        }).ToList();
        model.ChunkingStrategies = catalog.ChunkingStrategies.Select(item => new SelectListItem
        {
            Value = item.Id.ToString(),
            Text = $"{item.Name} - {item.Provider} {item.ModelId}",
            Selected = model.ChunkingStrategyIds.Contains(item.Id)
        }).ToList();

        return model;
    }

    private static IReadOnlyList<ResearchQuestionInput> ParseQuestions(string? questionsText)
    {
        if (string.IsNullOrWhiteSpace(questionsText))
        {
            return Array.Empty<ResearchQuestionInput>();
        }

        return questionsText
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .Where(parts => !string.IsNullOrWhiteSpace(parts[0]))
            .Select(parts => new ResearchQuestionInput
            {
                Question = parts[0],
                GroundTruth = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : parts[0],
                Difficulty = "Medium"
            })
            .ToList();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        cleaned = string.Join("-", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? "experiment" : cleaned.ToLowerInvariant();
    }
}
