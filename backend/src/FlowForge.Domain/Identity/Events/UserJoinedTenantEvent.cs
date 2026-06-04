using FlowForge.Domain.Identity.Enums;
using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Identity.Events;

public sealed record UserJoinedTenantEvent(Guid UserId, Guid TenantId, MembershipRole Role) : DomainEvent;
