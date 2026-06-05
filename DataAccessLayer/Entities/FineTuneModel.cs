using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class FineTuneModel
{
    public Guid Id { get; set; }
    public Guid ExperimentId { get; set; }
    public string BaseModel { get; set; } = string.Empty;
    public string? FineTunedModelId { get; set; }
    public FineTuneModelStatus Status { get; set; } = FineTuneModelStatus.Training;
    public string? TrainingConfig { get; set; }
    public string? TrainingMetrics { get; set; }
    public DateTime? TrainedAt { get; set; }

    public Experiment Experiment { get; set; } = null!;
}
