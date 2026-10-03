using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Queries;

/// <summary>
/// Public (no login) lookup for the invite landing page. The token itself is the secret, so
/// this only reveals what's needed to decide between "create an account" and "sign in".
/// </summary>
public record GetInvitationPreviewQuery(string Token) : IRequest<Result<InvitationPreviewDto>>;

public class GetInvitationPreviewQueryHandler : IRequestHandler<GetInvitationPreviewQuery, Result<InvitationPreviewDto>>
{
    private readonly IUnitOfWork _uow;

    public GetInvitationPreviewQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<InvitationPreviewDto>> Handle(GetInvitationPreviewQuery request, CancellationToken ct)
    {
        var invitation = await _uow.Invitations.GetByTokenAsync(request.Token, ct);
        if (invitation == null)
            return Result.Failure<InvitationPreviewDto>(Error.NotFound("Invitation.NotFound", "This invitation link isn't valid"));

        var tenant = await _uow.Tenants.GetByIdAsync(invitation.TenantId, ct);
        var inviter = await _uow.Users.GetByIdAsync(invitation.InvitedById, ct);
        var accountExists = await _uow.Users.EmailExistsAsync(invitation.Email, ct);

        var status = invitation.RevokedAt != null ? "revoked"
            : invitation.AcceptedAt != null ? "accepted"
            : invitation.IsExpired ? "expired"
            : "pending";

        return Result.Success(new InvitationPreviewDto(
            tenant?.Name ?? "a workspace", invitation.Email.Value, invitation.Role.ToString(),
            inviter?.FullName ?? "A teammate", status, accountExists));
    }
}
