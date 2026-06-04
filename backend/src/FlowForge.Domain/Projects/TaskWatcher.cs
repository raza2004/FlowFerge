namespace FlowForge.Domain.Projects;

public sealed class TaskWatcher
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
