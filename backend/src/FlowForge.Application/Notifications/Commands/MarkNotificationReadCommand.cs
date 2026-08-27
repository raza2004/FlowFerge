using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Notifications.Commands;

public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public MarkNotificationReadCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var notification = await _uow.Notifications.GetByIdAsync(request.NotificationId, ct);
        if (notification == null ||
            notification.TenantId != _currentUser.TenantId.Value ||
            notification.UserId != _currentUser.UserId.Value)
            return Result.Failure(Error.NotFound("Notification.NotFound", "Notification not found"));

        notification.MarkRead();
        _uow.Notifications.Update(notification);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
