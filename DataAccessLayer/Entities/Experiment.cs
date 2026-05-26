using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class Experiment
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ExperimentType ExperimentType { get; set; }
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Draft;
    public string? Config { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public ICollection<ExperimentRun> Runs { get; set; } = [];
    public ICollection<FineTuneModel> FineTuneModels { get; set; } = [];
}
