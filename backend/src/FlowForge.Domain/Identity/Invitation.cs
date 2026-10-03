using System.Security.Cryptography;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.ValueObjects;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Identity;

/// <summary>
/// An invitation for an email address to join a workspace. Separate from Membership because
/// the invitee may not have an account yet - a Membership needs a real user, an Invitation
/// only needs an email. Accepting it is what creates (or reactivates) the Membership.
/// </summary>
public sealed class Invitation : Entity
{
    public const int DefaultDaysValid = 7;

    public Guid TenantId { get; private set; }
    public Email Email { get; private set; } = null!;
    public MembershipRole Role { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public Guid InvitedById { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsPending => AcceptedAt == null && RevokedAt == null && !IsExpired;

    private Invitation() { }

    public static Result<Invitation> Create(Guid tenantId, Email email, MembershipRole role, Guid invitedById, int daysValid = DefaultDaysValid)
    {
        if (role == MembershipRole.Owner)
            return Result.Failure<Invitation>(Error.Validation("Invitation.InvalidRole", "A workspace can only have one owner"));

        return Result.Success(new Invitation
        {
            TenantId = tenantId,
            Email = email,
            Role = role,
            InvitedById = invitedById,
            Token = GenerateToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(daysValid)
        });
    }

    public Result Accept(Guid userId)
    {
        if (RevokedAt != null)
            return Result.Failure(Error.Validation("Invitation.Revoked", "This invitation was revoked"));
        if (AcceptedAt != null)
            return Result.Failure(Error.Conflict("Invitation.AlreadyAccepted", "This invitation was already used"));
        if (IsExpired)
            return Result.Failure(Error.Validation("Invitation.Expired", "This invitation has expired"));

        AcceptedAt = DateTime.UtcNow;
        AcceptedByUserId = userId;
        Touch();
        return Result.Success();
    }

    public void Revoke()
    {
        if (RevokedAt != null || AcceptedAt != null) return;
        RevokedAt = DateTime.UtcNow;
        Touch();
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
}
