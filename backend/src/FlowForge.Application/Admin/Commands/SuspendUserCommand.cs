using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Commands;

public record SuspendUserCommand(Guid UserId) : IRequest<Result>;

public class SuspendUserCommandHandler : IRequestHandler<SuspendUserCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public SuspendUserCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SuspendUserCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        if (request.UserId == _currentUser.UserId)
            return Result.Failure(Error.Validation("Admin.CannotSuspendSelf", "You cannot suspend your own account"));

        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return Result.Failure(Error.NotFound("User.NotFound", "User not found"));

        user.Suspend();
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
