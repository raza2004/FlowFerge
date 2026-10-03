// Mirrors FlowForge.Domain.Notifications.Enums.NotificationType exactly.
export enum NotificationType {
  TaskAssigned = 0,
  AutomationTriggered = 1,
  Mentioned = 2,
  CommentAdded = 3
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
