using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class LabelRepository : ILabelRepository
{
    private readonly FlowForgeDbContext _ctx;
    public LabelRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Label?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Labels.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IEnumerable<Label>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await _ctx.Labels.Where(l => l.ProjectId == projectId).ToListAsync(ct);

    public async Task AddAsync(Label label, CancellationToken ct = default) =>
        await _ctx.Labels.AddAsync(label, ct);

    public void Update(Label label) => _ctx.Labels.Update(label);
}
