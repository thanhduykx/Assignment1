using DataAccessLayer;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PresentationLayer.Models;

public sealed class ResearchIndexViewModel
{
    public IReadOnlyList<ResearchExperimentSummary> Experiments { get; set; } = Array.Empty<ResearchExperimentSummary>();
}

public sealed class ResearchCreateViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string QuestionsText { get; set; } = string.Empty;
    public List<Guid> EmbeddingModelIds { get; set; } = new();
    public List<Guid> ChunkingStrategyIds { get; set; } = new();
    public bool UseLocalFineTunedBaseline { get; set; } = true;
    public string? FineTunedTrainingText { get; set; }
    public string? FineTunedModelName { get; set; }
    public string? FineTunedEndpoint { get; set; }
    public IReadOnlyList<SelectListItem> EmbeddingModels { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ChunkingStrategies { get; set; } = Array.Empty<SelectListItem>();
}

public sealed class ResearchDetailViewModel
{
    public ResearchExperimentDetail Experiment { get; set; } = new();
}
