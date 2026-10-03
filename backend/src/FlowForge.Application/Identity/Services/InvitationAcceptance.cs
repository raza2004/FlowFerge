using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Identity.Services;

public static class InvitationAcceptance
{
    /// <summary>
    /// Marks the invitation used and gives the user an active membership with the invited role,
    /// reusing their old membership if they were previously removed from this workspace.
    /// Does not save - the caller saves as part of its own unit of work.
    /// </summary>
    public static async Task<Result<Membership>> JoinAsync(IUnitOfWork uow, Invitation invitation, User user, CancellationToken ct)
    {
        var tenant = await uow.Tenants.GetByIdAsync(invitation.TenantId, ct);
        if (tenant == null || !tenant.IsActive)
            return Result.Failure<Membership>(Error.Validation("Invitation.WorkspaceUnavailable", "This workspace is no longer available"));

        var acceptResult = invitation.Accept(user.Id);
        if (acceptResult.IsFailure) return Result.Failure<Membership>(acceptResult.Error);

        var existing = await uow.Memberships.GetByUserAndTenantAsync(user.Id, invitation.TenantId, ct);
        if (existing != null)
        {
            if (!existing.IsActive) existing.Reactivate(invitation.Role);
            return Result.Success(existing);
        }

        // Who invited whom is recorded on the Invitation itself (InvitedById / AcceptedByUserId),
        // so the membership is created as already accepted.
        var membershipResult = Membership.Create(user.Id, invitation.TenantId, invitation.Role);
        if (membershipResult.IsFailure) return membershipResult;

        await uow.Memberships.AddAsync(membershipResult.Value, ct);
        return membershipResult;
    }
}
