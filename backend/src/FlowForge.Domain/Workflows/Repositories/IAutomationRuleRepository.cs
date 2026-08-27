namespace FlowForge.Domain.Workflows.Repositories;

public interface IAutomationRuleRepository
{
    Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<AutomationRule>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<AutomationRule>> GetEnabledByTriggerAsync(Guid projectId, Guid triggerListId, CancellationToken ct = default);
    Task AddAsync(AutomationRule rule, CancellationToken ct = default);
    void Update(AutomationRule rule);
}
