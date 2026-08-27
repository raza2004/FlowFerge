using FlowForge.Domain.Notifications.Enums;

namespace FlowForge.Application.Notifications.DTOs;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    Guid? RelatedTaskId,
    Guid? RelatedProjectId,
    bool IsRead,
    DateTime CreatedAt
);
