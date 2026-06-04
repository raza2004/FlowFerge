using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects.Events;

public sealed record TaskMovedEvent(Guid TaskId, Guid FromListId, Guid ToListId, Guid MovedById, Guid TenantId) : DomainEvent;
