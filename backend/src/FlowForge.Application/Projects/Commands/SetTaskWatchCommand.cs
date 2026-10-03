using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

/// <summary>Watches (Watch = true) or unwatches a task for the current user.</summary>
public record SetTaskWatchCommand(Guid TaskId, bool Watch) : IRequest<Result>;

public class SetTaskWatchCommandHandler : IRequestHandler<SetTaskWatchCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public SetTaskWatchCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetTaskWatchCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdWithDetailsAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        var userId = _currentUser.UserId.Value;
        if (request.Watch) task.AddWatcher(userId);
        else task.RemoveWatcher(userId);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
