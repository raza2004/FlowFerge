using FlowForge.Domain.Workflows;
using FlowForge.Domain.Workflows.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class AutomationRuleRepository : IAutomationRuleRepository
{
    private readonly FlowForgeDbContext _ctx;
    public AutomationRuleRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IEnumerable<AutomationRule>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await _ctx.AutomationRules.Where(r => r.ProjectId == projectId).ToListAsync(ct);

    public async Task<IEnumerable<AutomationRule>> GetEnabledByTriggerAsync(Guid projectId, Guid triggerListId, CancellationToken ct = default) =>
        await _ctx.AutomationRules
            .Where(r => r.ProjectId == projectId && r.TriggerListId == triggerListId && r.IsEnabled)
            .ToListAsync(ct);

    public async Task AddAsync(AutomationRule rule, CancellationToken ct = default) =>
        await _ctx.AutomationRules.AddAsync(rule, ct);

    public void Update(AutomationRule rule) => _ctx.AutomationRules.Update(rule);
}
