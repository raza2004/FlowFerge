namespace FlowForge.Application.Common.Abstractions;

/// <summary>
/// Pushes a real-time event to a single user's connected clients.
/// Implemented in the API layer (SignalR is a presentation-layer concern) and
/// injected here only as an abstraction, keeping Application free of transport details.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default);
}
