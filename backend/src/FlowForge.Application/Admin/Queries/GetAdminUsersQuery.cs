using FlowForge.Application.Admin.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Queries;

public record GetAdminUsersQuery : IRequest<Result<List<AdminUserDto>>>;

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, Result<List<AdminUserDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetAdminUsersQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AdminUserDto>>> Handle(GetAdminUsersQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure<List<AdminUserDto>>(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var users = await _uow.Users.GetAllAsync(ct: ct);

        var result = users.Select(u => new AdminUserDto(
            u.Id, u.Email.Value, u.FullName, u.IsSystemAdmin, u.IsEmailVerified,
            u.Status.ToString(), u.CreatedAt, u.LastLoginAt
        )).ToList();

        return Result.Success(result);
    }
}
