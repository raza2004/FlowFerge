using FlowForge.Application.Admin.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Queries;

public record GetAdminStatsQuery : IRequest<Result<AdminStatsDto>>;

public class GetAdminStatsQueryHandler : IRequestHandler<GetAdminStatsQuery, Result<AdminStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetAdminStatsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AdminStatsDto>> Handle(GetAdminStatsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure<AdminStatsDto>(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var tenants = (await _uow.Tenants.GetAllAsync(ct)).ToList();
        var users = (await _uow.Users.GetAllAsync(take: int.MaxValue, ct: ct)).ToList();

        var totalProjects = 0;
        foreach (var t in tenants)
            totalProjects += (await _uow.Projects.GetByTenantAsync(t.Id, includeArchived: true, ct: ct)).Count();

        return Result.Success(new AdminStatsDto(
            TotalTenants: tenants.Count,
            ActiveTenants: tenants.Count(t => t.IsActive),
            TotalUsers: users.Count,
            TotalProjects: totalProjects
        ));
    }
}
