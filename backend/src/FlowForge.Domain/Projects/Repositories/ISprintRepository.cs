namespace FlowForge.Domain.Projects.Repositories;

public interface ISprintRepository
{
    Task<Sprint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Sprint?> GetActiveSprintAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Sprint sprint, CancellationToken ct = default);
    void Update(Sprint sprint);
}
