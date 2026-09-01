using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record UpdateNotificationPreferencesCommand(bool EmailNotificationsEnabled) : IRequest<Result>;

public class UpdateNotificationPreferencesCommandHandler : IRequestHandler<UpdateNotificationPreferencesCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateNotificationPreferencesCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateNotificationPreferencesCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NotAuthenticated", "Not authenticated"));

        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
        if (user == null)
            return Result.Failure(Error.NotFound("User.NotFound", "User not found"));

        user.SetEmailNotificationsEnabled(request.EmailNotificationsEnabled);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
