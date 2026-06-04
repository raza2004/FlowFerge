using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects.Events;

public sealed record ProjectCreatedEvent(Guid ProjectId, Guid TenantId, string Name, Guid CreatedById) : DomainEvent;
