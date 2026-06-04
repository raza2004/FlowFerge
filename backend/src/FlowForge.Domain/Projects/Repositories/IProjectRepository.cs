using FlowForge.Domain.Projects.ValueObjects;

namespace FlowForge.Domain.Projects.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetByKeyAsync(Guid tenantId, ProjectKey key, CancellationToken ct = default);
    Task<bool> KeyExistsAsync(Guid tenantId, ProjectKey key, CancellationToken ct = default);
    Task<IEnumerable<Project>> GetByTenantAsync(Guid tenantId, bool includeArchived = false, CancellationToken ct = default);
    Task<IEnumerable<Project>> GetByMemberAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    void Update(Project project);
}
