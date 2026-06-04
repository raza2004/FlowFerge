using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class TaskCommentRepository : ITaskCommentRepository
{
    private readonly FlowForgeDbContext _ctx;
    public TaskCommentRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<TaskComment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.TaskComments.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<TaskComment>> GetByTaskAsync(Guid taskId, CancellationToken ct = default) =>
        await _ctx.TaskComments.Where(c => c.TaskId == taskId).OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(TaskComment comment, CancellationToken ct = default) =>
        await _ctx.TaskComments.AddAsync(comment, ct);

    public void Update(TaskComment comment) => _ctx.TaskComments.Update(comment);
}
