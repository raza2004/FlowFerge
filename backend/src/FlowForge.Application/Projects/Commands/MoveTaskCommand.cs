using MediatR;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Projects.Commands;

public record MoveTaskCommand(
    Guid TaskId,
    Guid NewListId,
    int NewPosition
) : IRequest<Result>;

public class MoveTaskCommandHandler : IRequestHandler<MoveTaskCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public MoveTaskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(MoveTaskCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        var moveResult = task.MoveTo(request.NewListId, request.NewPosition,
            _currentUser.UserId.Value, _currentUser.TenantId.Value);
        if (moveResult.IsFailure) return moveResult;

        _uow.Tasks.Update(task);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
