using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Queries;

public record GetCurrentUserQuery : IRequest<Result<CurrentUserResponse>>;

public record CurrentUserResponse(
    UserDto User,
    List<MembershipDto> Memberships,
    TenantDto? ActiveTenant
);

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;

    public GetCurrentUserQueryHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == null)
            return Result.Failure<CurrentUserResponse>(Error.Unauthorized("Auth.NotAuthenticated", "Not authenticated"));

        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
        if (user == null)
            return Result.Failure<CurrentUserResponse>(Error.NotFound("User.NotFound", "User not found"));

        var memberships = await _uow.Memberships.GetByUserAsync(user.Id, ct);
        var membershipDtos = new List<MembershipDto>();
        TenantDto? activeTenant = null;

        foreach (var m in memberships.Where(x => x.IsActive))
        {
            var tenant = await _uow.Tenants.GetByIdAsync(m.TenantId, ct);
            if (tenant == null) continue;

            membershipDtos.Add(new MembershipDto(tenant.Id, tenant.Name, tenant.Slug, m.Role.ToString(), m.JoinedAt));

            if (_currentUser.TenantId == tenant.Id)
                activeTenant = new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl, tenant.PlanTier, tenant.IsActive);
        }

        return Result.Success(new CurrentUserResponse(
            new UserDto(user.Id, user.Email.Value, user.FirstName, user.LastName, user.FullName, user.AvatarUrl, user.IsSystemAdmin, user.IsEmailVerified),
            membershipDtos,
            activeTenant
        ));
    }
}
