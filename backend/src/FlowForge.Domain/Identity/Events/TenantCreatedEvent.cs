using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Identity.Events;

public sealed record TenantCreatedEvent(Guid TenantId, string Name, Guid OwnerId) : DomainEvent;
