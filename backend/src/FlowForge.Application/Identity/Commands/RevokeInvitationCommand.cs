using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record RevokeInvitationCommand(Guid InvitationId) : IRequest<Result>;

public class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public RevokeInvitationCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RevokeInvitationCommand request, CancellationToken ct)
    {
        var access = await WorkspaceAccess.RequireAdminAsync(_uow, _currentUser, ct);
        if (access.IsFailure) return access;

        var invitation = await _uow.Invitations.GetByIdAsync(request.InvitationId, ct);
        if (invitation == null || invitation.TenantId != access.Value.TenantId)
            return Result.Failure(Error.NotFound("Invitation.NotFound", "Invitation not found"));

        invitation.Revoke();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
