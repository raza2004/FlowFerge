using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Queries;

public record GetPendingInvitationsQuery : IRequest<Result<List<InvitationDto>>>;

public class GetPendingInvitationsQueryHandler : IRequestHandler<GetPendingInvitationsQuery, Result<List<InvitationDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAppLinks _links;

    public GetPendingInvitationsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IAppLinks links)
    {
        _uow = uow;
        _currentUser = currentUser;
        _links = links;
    }

    public async Task<Result<List<InvitationDto>>> Handle(GetPendingInvitationsQuery request, CancellationToken ct)
    {
        var access = await WorkspaceAccess.RequireAdminAsync(_uow, _currentUser, ct);
        if (access.IsFailure) return Result.Failure<List<InvitationDto>>(access.Error);

        var tenantId = access.Value.TenantId;
        var invitations = await _uow.Invitations.GetPendingByTenantAsync(tenantId, ct);
        var names = (await _uow.Users.GetByTenantAsync(tenantId, ct)).ToDictionary(u => u.Id, u => u.FullName);

        return Result.Success(invitations.Select(i => new InvitationDto(
            i.Id, i.Email.Value, i.Role.ToString(),
            names.GetValueOrDefault(i.InvitedById, "A former member"),
            i.CreatedAt, i.ExpiresAt, _links.InvitationUrl(i.Token))).ToList());
    }
}
