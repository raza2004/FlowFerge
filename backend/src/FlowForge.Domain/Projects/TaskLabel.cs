namespace FlowForge.Domain.Projects;

public sealed class TaskLabel
{
    public Guid TaskId { get; set; }
    public Guid LabelId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
