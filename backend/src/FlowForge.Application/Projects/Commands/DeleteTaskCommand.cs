using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

/// <summary>
/// Soft-deletes a task and its subtasks. The global query filter on tasks hides them
/// everywhere afterwards (board, dashboard, My Work), but the rows stay for the audit trail.
/// </summary>
public record DeleteTaskCommand(Guid TaskId) : IRequest<Result>;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteTaskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdWithDetailsAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        var userId = _currentUser.UserId.Value;
        foreach (var subtask in task.Subtasks)
            subtask.SoftDelete(userId);
        task.SoftDelete(userId);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
