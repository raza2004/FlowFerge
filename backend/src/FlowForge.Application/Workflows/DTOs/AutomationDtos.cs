using FlowForge.Domain.Workflows.Enums;

namespace FlowForge.Application.Workflows.DTOs;

public record CreateAutomationRuleRequest(
    string Name,
    AutomationTriggerType TriggerType,
    Guid TriggerListId,
    AutomationActionType ActionType,
    Guid ActionUserId
);

public record AutomationRuleDto(
    Guid Id,
    string Name,
    AutomationTriggerType TriggerType,
    Guid TriggerListId,
    string TriggerListName,
    AutomationActionType ActionType,
    Guid ActionUserId,
    string ActionUserName,
    bool IsEnabled,
    DateTime CreatedAt
);
