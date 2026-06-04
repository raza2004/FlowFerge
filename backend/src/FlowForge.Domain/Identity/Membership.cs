using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.Events;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Identity;

public sealed class Membership : Entity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public MembershipRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public Guid? InvitedById { get; private set; }
    public string? InvitationToken { get; private set; }
    public DateTime? InvitationAcceptedAt { get; private set; }

    public User User { get; private set; } = null!;
    public Tenant Tenant { get; private set; } = null!;

    private Membership() { }

    public static Result<Membership> Create(Guid userId, Guid tenantId, MembershipRole role, Guid? invitedById = null)
    {
        var membership = new Membership
        {
            UserId = userId,
            TenantId = tenantId,
            Role = role,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            InvitedById = invitedById,
            InvitationAcceptedAt = invitedById == null ? DateTime.UtcNow : null,
            InvitationToken = invitedById != null ? Guid.NewGuid().ToString("N") : null
        };

        membership.RaiseDomainEvent(new UserJoinedTenantEvent(userId, tenantId, role));
        return Result.Success(membership);
    }

    public Result ChangeRole(MembershipRole newRole, MembershipRole changedByRole)
    {
        if (Role == MembershipRole.Owner && newRole != MembershipRole.Owner)
            return Result.Failure(Error.Forbidden("Membership.CannotDemoteOwner", "Cannot demote tenant owner"));

        if (changedByRole > MembershipRole.Admin)
            return Result.Failure(Error.Forbidden("Membership.InsufficientPermission", "Only Owner or Admin can change roles"));

        Role = newRole;
        Touch();
        return Result.Success();
    }

    public Result AcceptInvitation(string token)
    {
        if (InvitationToken != token)
            return Result.Failure(Error.Validation("Membership.InvalidInvitation", "Invitation token is invalid"));

        InvitationToken = null;
        InvitationAcceptedAt = DateTime.UtcNow;
        IsActive = true;
        Touch();
        return Result.Success();
    }

    public void Deactivate()
    {
        IsActive = false;
        LeftAt = DateTime.UtcNow;
        Touch();
    }

    public bool HasPermission(MembershipRole minimumRole) => Role <= minimumRole;
}
