using FlowForge.Domain.Notifications.Enums;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Notifications;

public sealed class Notification : TenantEntity
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public Guid? RelatedTaskId { get; private set; }
    public Guid? RelatedProjectId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification() { }

    public static Result<Notification> Create(
        Guid tenantId,
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid? relatedTaskId = null,
        Guid? relatedProjectId = null)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return Result.Failure<Notification>(Error.Validation("Notification.InvalidTitle", "Title must be 1-200 chars"));

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Message = message.Trim(),
            RelatedTaskId = relatedTaskId,
            RelatedProjectId = relatedProjectId,
            IsRead = false
        };

        notification.GetType().GetProperty("TenantId")!.SetValue(notification, tenantId);
        return Result.Success(notification);
    }

    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
        Touch();
    }
}
