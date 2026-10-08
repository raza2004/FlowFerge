namespace FlowForge.Domain.Notifications.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Notification>> GetByUserAsync(Guid userId, Guid tenantId, int take = 50, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    /// <summary>Whether this user already got a notification of this type about this task since the given time.</summary>
    Task<bool> ExistsAsync(Guid userId, Guid relatedTaskId, FlowForge.Domain.Notifications.Enums.NotificationType type, DateTime since, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);
    void Update(Notification notification);
}
