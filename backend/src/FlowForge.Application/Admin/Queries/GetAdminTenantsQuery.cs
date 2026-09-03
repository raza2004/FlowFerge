using FlowForge.Application.Admin.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Queries;

public record GetAdminTenantsQuery : IRequest<Result<List<AdminTenantDto>>>;

public class GetAdminTenantsQueryHandler : IRequestHandler<GetAdminTenantsQuery, Result<List<AdminTenantDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetAdminTenantsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AdminTenantDto>>> Handle(GetAdminTenantsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure<List<AdminTenantDto>>(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var tenants = await _uow.Tenants.GetAllAsync(ct);
        var result = new List<AdminTenantDto>();

        foreach (var t in tenants)
        {
            var members = await _uow.Memberships.GetByTenantAsync(t.Id, ct);
            var projects = await _uow.Projects.GetByTenantAsync(t.Id, includeArchived: true, ct: ct);

            result.Add(new AdminTenantDto(
                t.Id, t.Name, t.Slug, t.PlanTier, t.IsActive, t.SuspensionReason,
                members.Count(m => m.IsActive), projects.Count(), t.CreatedAt));
        }

        return Result.Success(result.OrderByDescending(t => t.CreatedAt).ToList());
    }
}
