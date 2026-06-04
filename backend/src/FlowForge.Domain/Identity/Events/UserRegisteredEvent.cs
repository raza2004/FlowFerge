using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Identity.Events;

public sealed record UserRegisteredEvent(Guid UserId, string Email, Guid TenantId) : DomainEvent;
