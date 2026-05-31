using DataAccessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PresentationLayer.Models;
using ServicesLayer;

namespace PresentationLayer.Controllers;

[Authorize]
public sealed class ResearchController : Controller
{
    private readonly IResearchBenchmarkService _researchService;
    private readonly ILogger<ResearchController> _logger;

    public ResearchController(IResearchBenchmarkService researchService, ILogger<ResearchController> logger)
    {
        _researchService = researchService;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _researchService.RunExperimentAsync(id, cancellationToken);
            TempData["Success"] = "Benchmark completed.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Research benchmark failed");
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<ResearchCreateViewModel> BuildCreateViewModelAsync(ResearchCreateViewModel model, CancellationToken cancellationToken)
    {
        var catalog = await _researchService.GetCatalogAsync(cancellationToken);
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
}
