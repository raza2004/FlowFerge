using MediatR;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Identity.Queries;

public record GetTenantMembersQuery : IRequest<Result<List<TenantMemberDto>>>;

public class GetTenantMembersQueryHandler
    : IRequestHandler<GetTenantMembersQuery, Result<List<TenantMemberDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetTenantMembersQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<TenantMemberDto>>> Handle(GetTenantMembersQuery req, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<TenantMemberDto>>(Error.Unauthorized("Auth.NoTenant", "No tenant"));

        var memberships = await _uow.Memberships.GetByTenantAsync(_currentUser.TenantId.Value, ct);

        return Result.Success(memberships
            .Where(m => m.IsActive)
            .Select(m => new TenantMemberDto(
                m.UserId,
                m.User.FullName,
                m.User.Email.Value,
                m.User.AvatarUrl,
                m.Role.ToString(),
                m.JoinedAt
            )).ToList());
    }
}
