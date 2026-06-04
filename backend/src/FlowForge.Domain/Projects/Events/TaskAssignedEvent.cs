using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects.Events;

public sealed record TaskAssignedEvent(Guid TaskId, Guid AssigneeId, Guid AssignedById, Guid TenantId) : DomainEvent;
