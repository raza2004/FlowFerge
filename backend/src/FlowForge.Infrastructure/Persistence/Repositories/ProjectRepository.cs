using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Repositories;
using FlowForge.Domain.Projects.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly FlowForgeDbContext _ctx;
    public ProjectRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Projects
            .Include(p => p.Boards).ThenInclude(b => b.Lists).ThenInclude(l => l.Tasks)
            .Include(p => p.Members)
            .Include(p => p.Labels)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetByKeyAsync(Guid tenantId, ProjectKey key, CancellationToken ct = default) =>
        _ctx.Projects.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Key == key, ct);

    public Task<bool> KeyExistsAsync(Guid tenantId, ProjectKey key, CancellationToken ct = default) =>
        _ctx.Projects.AnyAsync(p => p.TenantId == tenantId && p.Key == key, ct);

    public async Task<IEnumerable<Project>> GetByTenantAsync(Guid tenantId, bool includeArchived = false, CancellationToken ct = default) =>
        await _ctx.Projects
            .Where(p => p.TenantId == tenantId)
            .Where(p => includeArchived || p.ArchivedAt == null)
            .ToListAsync(ct);

    public async Task<IEnumerable<Project>> GetByMemberAsync(Guid userId, Guid tenantId, CancellationToken ct = default) =>
        await _ctx.Projects
            .Where(p => p.TenantId == tenantId)
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .ToListAsync(ct);

    public async Task AddAsync(Project project, CancellationToken ct = default) =>
        await _ctx.Projects.AddAsync(project, ct);

    public void Update(Project project) => _ctx.Projects.Update(project);
}
