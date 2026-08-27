using FlowForge.Domain.Workflows.Enums;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Workflows;

/// <summary>
/// A project-scoped "when X happens, do Y" rule.
/// Evaluated by <c>TaskMovedAutomationHandler</c> when the matching domain event fires.
/// </summary>
public sealed class AutomationRule : SoftDeletableEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public AutomationTriggerType TriggerType { get; private set; }
    public Guid TriggerListId { get; private set; }

    public AutomationActionType ActionType { get; private set; }
    public Guid ActionUserId { get; private set; }

    public bool IsEnabled { get; private set; } = true;
    public Guid CreatedById { get; private set; }

    private AutomationRule() { }

    public static Result<AutomationRule> Create(
        Guid tenantId,
        Guid projectId,
        string name,
        AutomationTriggerType triggerType,
        Guid triggerListId,
        AutomationActionType actionType,
        Guid actionUserId,
        Guid createdById)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return Result.Failure<AutomationRule>(Error.Validation("Automation.InvalidName", "Name must be 1-150 chars"));

        if (triggerListId == Guid.Empty)
            return Result.Failure<AutomationRule>(Error.Validation("Automation.InvalidTrigger", "A trigger list is required"));

        if (actionUserId == Guid.Empty)
            return Result.Failure<AutomationRule>(Error.Validation("Automation.InvalidAction", "An action target user is required"));

        var rule = new AutomationRule
        {
            ProjectId = projectId,
            Name = name.Trim(),
            TriggerType = triggerType,
            TriggerListId = triggerListId,
            ActionType = actionType,
            ActionUserId = actionUserId,
            CreatedById = createdById,
            IsEnabled = true
        };

        rule.GetType().GetProperty("TenantId")!.SetValue(rule, tenantId);
        return Result.Success(rule);
    }

    public void Enable()
    {
        IsEnabled = true;
        Touch();
    }

    public void Disable()
    {
        IsEnabled = false;
        Touch();
    }
}
