// Mirrors FlowForge.Domain.Workflows.Enums exactly - System.Text.Json serializes
// C# enums as their ordinal by default, so these numeric values must stay in sync
// with the backend enum declarations.
export enum AutomationTriggerType {
  TaskMovedToList = 0
}

export enum AutomationActionType {
  NotifyUser = 0,
  AssignUser = 1
}

export interface AutomationRuleDto {
  id: string;
  name: string;
  triggerType: AutomationTriggerType;
  triggerListId: string;
  triggerListName: string;
  actionType: AutomationActionType;
  actionUserId: string;
  actionUserName: string;
  isEnabled: boolean;
  createdAt: string;
}

export interface CreateAutomationRuleRequest {
  name: string;
  triggerType: AutomationTriggerType;
  triggerListId: string;
  actionType: AutomationActionType;
  actionUserId: string;
}
