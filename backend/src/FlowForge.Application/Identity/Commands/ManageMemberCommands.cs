using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record ChangeMemberRoleCommand(Guid UserId, string Role) : IRequest<Result>;

/// <summary>
/// Removes someone from the current workspace. Their membership is deactivated rather than
/// deleted, so their past comments and tasks keep their author, and a later invite restores them.
/// </summary>
public record RemoveMemberCommand(Guid UserId) : IRequest<Result>;

internal static class MemberManagement
{
    /// <summary>
    /// Owners can manage anyone but themselves; admins can manage members below admin.
    /// Nobody can change the owner or their own membership through these commands.
    /// </summary>
    public static async Task<Result<(Membership Actor, Membership Target)>> LoadAsync(
        IUnitOfWork uow, ICurrentUser currentUser, Guid targetUserId, CancellationToken ct)
    {
        var access = await WorkspaceAccess.RequireAdminAsync(uow, currentUser, ct);
        if (access.IsFailure) return Result.Failure<(Membership, Membership)>(access.Error);
        var actor = access.Value;

        if (targetUserId == actor.UserId)
            return Result.Failure<(Membership, Membership)>(Error.Validation("Workspace.Self", "You can't change your own membership"));

        var target = await uow.Memberships.GetByUserAndTenantAsync(targetUserId, actor.TenantId, ct);
        if (target == null || !target.IsActive)
            return Result.Failure<(Membership, Membership)>(Error.NotFound("Workspace.MemberNotFound", "Member not found"));

        if (target.Role == MembershipRole.Owner)
            return Result.Failure<(Membership, Membership)>(Error.Forbidden("Workspace.Owner", "The workspace owner can't be changed or removed"));

        if (actor.Role != MembershipRole.Owner && target.Role <= actor.Role)
            return Result.Failure<(Membership, Membership)>(Error.Forbidden("Workspace.InsufficientRole", "Only the owner can manage other admins"));

        return Result.Success((actor, target));
    }
}

public class ChangeMemberRoleCommandHandler : IRequestHandler<ChangeMemberRoleCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ChangeMemberRoleCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ChangeMemberRoleCommand request, CancellationToken ct)
    {
        var loaded = await MemberManagement.LoadAsync(_uow, _currentUser, request.UserId, ct);
        if (loaded.IsFailure) return loaded;
        var (actor, target) = loaded.Value;

        var roleResult = WorkspaceAccess.ParseAssignableRole(request.Role);
        if (roleResult.IsFailure) return roleResult;

        if (roleResult.Value < actor.Role)
            return Result.Failure(Error.Forbidden("Workspace.RoleTooHigh", "You can't grant a higher role than your own"));

        var changeResult = target.ChangeRole(roleResult.Value, actor.Role);
        if (changeResult.IsFailure) return changeResult;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public RemoveMemberCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RemoveMemberCommand request, CancellationToken ct)
    {
        var loaded = await MemberManagement.LoadAsync(_uow, _currentUser, request.UserId, ct);
        if (loaded.IsFailure) return loaded;

        loaded.Value.Target.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
