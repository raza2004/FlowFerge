using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Queries;

/// <summary>Workspaces the user can switch to (active membership in a non-suspended workspace).</summary>
public record GetMyWorkspacesQuery : IRequest<Result<List<WorkspaceDto>>>;

public class GetMyWorkspacesQueryHandler : IRequestHandler<GetMyWorkspacesQuery, Result<List<WorkspaceDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetMyWorkspacesQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<WorkspaceDto>>> Handle(GetMyWorkspacesQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
            return Result.Failure<List<WorkspaceDto>>(Error.Unauthorized("Auth.Required", "Not signed in"));

        var memberships = await _uow.Memberships.GetByUserAsync(_currentUser.UserId.Value, ct);
        return Result.Success(memberships
            .Where(m => m.IsActive && m.Tenant.IsActive)
            .OrderBy(m => m.Tenant.Name)
            .Select(m => new WorkspaceDto(m.TenantId, m.Tenant.Name, m.Tenant.Slug, m.Role.ToString(), m.TenantId == _currentUser.TenantId))
            .ToList());
    }
}
