using FlowForge.Domain.Notifications;

namespace FlowForge.Application.Notifications.Services;

/// <summary>
/// Fans a single persisted Notification out to every channel its recipient/tenant has
/// enabled: always real-time (SignalR), plus email (per-user preference) and Slack
/// (per-tenant webhook) when configured. Any caller that creates a Notification should
/// route it through here instead of talking to IRealtimeNotifier directly, so every
/// notification-producing feature gets all three channels for free.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(Notification notification, CancellationToken ct = default);
}
