namespace FlowForge.Domain.Projects.Repositories;

public interface ITaskCommentRepository
{
    Task<TaskComment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<TaskComment>> GetByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task AddAsync(TaskComment comment, CancellationToken ct = default);
    void Update(TaskComment comment);
}
