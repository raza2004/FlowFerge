using FlowForge.API.Hubs;
using FlowForge.Application.Common.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace FlowForge.API.Realtime;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    public SignalRRealtimeNotifier(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default) =>
        _hub.Clients.Group($"user-{userId}").SendAsync(eventName, payload, ct);
}
