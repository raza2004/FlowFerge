using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FlowForge.API.Hubs;

/// <summary>
/// Every connection auto-joins a per-user group keyed by the user id, so
/// IRealtimeNotifier can push to a specific user without the client having to
/// join/leave anything itself.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // ASP.NET's JWT handler remaps some short claim names (like "sub") to their long
        // ClaimTypes URI by default - CurrentUser.cs already has to fall back the same way.
        var userId = Context.User?.FindFirstValue("sub") ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        await base.OnConnectedAsync();
    }
}
