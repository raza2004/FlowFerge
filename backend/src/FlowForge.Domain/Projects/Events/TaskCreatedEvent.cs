using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects.Events;

public sealed record TaskCreatedEvent(Guid TaskId, Guid ProjectId, Guid TenantId, string Title, Guid CreatedById) : DomainEvent;
