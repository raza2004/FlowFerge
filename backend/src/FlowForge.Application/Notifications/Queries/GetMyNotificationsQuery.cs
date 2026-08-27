using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Notifications.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Notifications.Queries;

public record GetMyNotificationsQuery : IRequest<Result<List<NotificationDto>>>;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, Result<List<NotificationDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetMyNotificationsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<NotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<List<NotificationDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var notifications = await _uow.Notifications.GetByUserAsync(
            _currentUser.UserId.Value, _currentUser.TenantId.Value, ct: ct);

        return Result.Success(notifications
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Message,
                n.RelatedTaskId, n.RelatedProjectId, n.IsRead, n.CreatedAt))
            .ToList());
    }
}
