using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects.Events;

public sealed record TaskStatusChangedEvent(Guid TaskId, string OldStatus, string NewStatus, Guid ChangedById, Guid TenantId) : DomainEvent;
