using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Commands;

public record ReactivateUserCommand(Guid UserId) : IRequest<Result>;

public class ReactivateUserCommandHandler : IRequestHandler<ReactivateUserCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ReactivateUserCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ReactivateUserCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return Result.Failure(Error.NotFound("User.NotFound", "User not found"));

        user.Reactivate();
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
