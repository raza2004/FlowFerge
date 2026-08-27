// Mirrors FlowForge.Domain.Notifications.Enums.NotificationType exactly.
export enum NotificationType {
  TaskAssigned = 0,
  AutomationTriggered = 1
}

export interface NotificationDto {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  relatedTaskId: string | null;
  relatedProjectId: string | null;
  isRead: boolean;
  createdAt: string;
}
