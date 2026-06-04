using FlowForge.Domain.Projects.Enums;

namespace FlowForge.Domain.Projects.Repositories;

public interface ITaskRepository
{
    Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectTask?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ProjectTask>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<ProjectTask>> GetByBoardAsync(Guid boardId, CancellationToken ct = default);
    Task<IEnumerable<ProjectTask>> GetByListAsync(Guid listId, CancellationToken ct = default);
    Task<IEnumerable<ProjectTask>> GetByAssigneeAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<ProjectTask>> GetBySprintAsync(Guid sprintId, CancellationToken ct = default);
    Task<IEnumerable<ProjectTask>> GetOverdueTasksAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetNextTaskNumberAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(ProjectTask task, CancellationToken ct = default);
    void Update(ProjectTask task);
}
