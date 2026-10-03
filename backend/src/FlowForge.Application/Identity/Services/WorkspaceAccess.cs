using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Identity.Services;

public static class WorkspaceAccess
{
    /// <summary>
    /// Returns the caller's membership if they're an Owner or Admin of their current workspace.
    /// Read from the database rather than the JWT's role claim, so a demotion applies immediately.
    /// </summary>
    public static async Task<Result<Membership>> RequireAdminAsync(IUnitOfWork uow, ICurrentUser currentUser, CancellationToken ct)
    {
        if (currentUser.TenantId == null || currentUser.UserId == null)
            return Result.Failure<Membership>(Error.Unauthorized("Auth.NoTenant", "No active workspace"));

        var membership = await uow.Memberships.GetByUserAndTenantAsync(currentUser.UserId.Value, currentUser.TenantId.Value, ct);
        if (membership == null || !membership.IsActive)
            return Result.Failure<Membership>(Error.Forbidden("Workspace.NotMember", "You're not a member of this workspace"));

        if (!membership.HasPermission(MembershipRole.Admin))
            return Result.Failure<Membership>(Error.Forbidden("Workspace.AdminOnly", "Only workspace owners and admins can do this"));

        return Result.Success(membership);
    }

    /// <summary>Parses a role name for inviting/assigning. Owner is never assignable - ownership isn't transferable here.</summary>
    public static Result<MembershipRole> ParseAssignableRole(string role)
    {
        if (!Enum.TryParse<MembershipRole>(role, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return Result.Failure<MembershipRole>(Error.Validation("Workspace.InvalidRole", $"'{role}' is not a valid role"));

        if (parsed == MembershipRole.Owner)
            return Result.Failure<MembershipRole>(Error.Validation("Workspace.InvalidRole", "A workspace can only have one owner"));

        return Result.Success(parsed);
    }
}
