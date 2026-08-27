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

        var board = await _uow.Boards.GetByIdWithListsAsync(task.BoardId, ct);
        var targetList = board?.Lists.FirstOrDefault(l => l.Id == request.NewListId);
        if (targetList == null)
            return Result.Failure(Error.NotFound("BoardList.NotFound", "Target list not found"));

        var moveResult = task.MoveTo(request.NewListId, request.NewPosition,
            _currentUser.UserId.Value, _currentUser.TenantId.Value);
        if (moveResult.IsFailure) return moveResult;

        // Keep the free-text Status (and CompletedAt / dashboard stats) in sync with
        // whichever list the task now sits in, driven by the list's IsDoneColumn flag
        // rather than string-matching the list name.
        var statusResult = task.ChangeStatus(targetList.Name, targetList.IsDoneColumn,
            _currentUser.UserId.Value, _currentUser.TenantId.Value);
        if (statusResult.IsFailure) return statusResult;

        _uow.Tasks.Update(task);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
