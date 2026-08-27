using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly FlowForgeDbContext _ctx;
    public NotificationRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IEnumerable<Notification>> GetByUserAsync(Guid userId, Guid tenantId, int take = 50, CancellationToken ct = default) =>
        await _ctx.Notifications
            .Where(n => n.UserId == userId && n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId, CancellationToken ct = default) =>
        _ctx.Notifications.CountAsync(n => n.UserId == userId && n.TenantId == tenantId && !n.IsRead, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default) =>
        await _ctx.Notifications.AddAsync(notification, ct);

    public void Update(Notification notification) => _ctx.Notifications.Update(notification);
}
