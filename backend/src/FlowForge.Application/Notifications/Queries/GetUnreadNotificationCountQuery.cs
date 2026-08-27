using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Notifications.Queries;

public record GetUnreadNotificationCountQuery : IRequest<Result<int>>;

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetUnreadNotificationCountQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<int>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var count = await _uow.Notifications.GetUnreadCountAsync(
            _currentUser.UserId.Value, _currentUser.TenantId.Value, ct);

        return Result.Success(count);
    }
}
