using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly FlowForgeDbContext _ctx;
    public TaskRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<ProjectTask?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Tasks
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .Include(t => t.Labels)
            .Include(t => t.Watchers)
            .Include(t => t.Subtasks)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IEnumerable<ProjectTask>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await _ctx.Tasks.Where(t => t.ProjectId == projectId).ToListAsync(ct);

    public async Task<IEnumerable<ProjectTask>> GetByBoardAsync(Guid boardId, CancellationToken ct = default) =>
        await _ctx.Tasks.Where(t => t.BoardId == boardId).OrderBy(t => t.Position).ToListAsync(ct);

    public async Task<IEnumerable<ProjectTask>> GetByListAsync(Guid listId, CancellationToken ct = default) =>
        await _ctx.Tasks.Where(t => t.ListId == listId).OrderBy(t => t.Position).ToListAsync(ct);

    public async Task<IEnumerable<ProjectTask>> GetByAssigneeAsync(Guid userId, Guid tenantId, CancellationToken ct = default) =>
        await _ctx.Tasks.Where(t => t.AssigneeId == userId && t.TenantId == tenantId).ToListAsync(ct);

    public async Task<IEnumerable<ProjectTask>> GetBySprintAsync(Guid sprintId, CancellationToken ct = default) =>
        await _ctx.Tasks.Where(t => t.SprintId == sprintId).ToListAsync(ct);

    public async Task<IEnumerable<ProjectTask>> GetOverdueTasksAsync(Guid tenantId, CancellationToken ct = default) =>
        await _ctx.Tasks
            .Where(t => t.TenantId == tenantId && t.DueDate != null && t.DueDate < DateTime.UtcNow && t.CompletedAt == null)
            .ToListAsync(ct);

    public async Task<int> GetNextTaskNumberAsync(Guid projectId, CancellationToken ct = default)
    {
        var count = await _ctx.Tasks.IgnoreQueryFilters().CountAsync(t => t.ProjectId == projectId, ct);
        return count + 1;
    }

    public async Task AddAsync(ProjectTask task, CancellationToken ct = default) =>
        await _ctx.Tasks.AddAsync(task, ct);

    public void Update(ProjectTask task) => _ctx.Tasks.Update(task);
}
