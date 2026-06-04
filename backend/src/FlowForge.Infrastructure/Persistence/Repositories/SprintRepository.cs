using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class SprintRepository : ISprintRepository
{
    private readonly FlowForgeDbContext _ctx;
    public SprintRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Sprint?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Sprints.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Sprint?> GetActiveSprintAsync(Guid projectId, CancellationToken ct = default) =>
        _ctx.Sprints.FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Status == SprintStatus.Active, ct);

    public async Task<IEnumerable<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await _ctx.Sprints.Where(s => s.ProjectId == projectId).OrderByDescending(s => s.StartDate).ToListAsync(ct);

    public async Task AddAsync(Sprint sprint, CancellationToken ct = default) =>
        await _ctx.Sprints.AddAsync(sprint, ct);

    public void Update(Sprint sprint) => _ctx.Sprints.Update(sprint);
}
