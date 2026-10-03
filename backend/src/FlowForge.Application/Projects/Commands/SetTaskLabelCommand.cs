using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

/// <summary>Attaches (Attach = true) or detaches a project label on a task.</summary>
public record SetTaskLabelCommand(Guid TaskId, Guid LabelId, bool Attach) : IRequest<Result>;

public class SetTaskLabelCommandHandler : IRequestHandler<SetTaskLabelCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public SetTaskLabelCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetTaskLabelCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdWithDetailsAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        var label = await _uow.Labels.GetByIdAsync(request.LabelId, ct);
        if (label == null || label.ProjectId != task.ProjectId)
            return Result.Failure(Error.NotFound("Label.NotFound", "Label not found in this project"));

        if (request.Attach)
        {
            if (task.Labels.Any(l => l.LabelId == label.Id)) return Result.Success();
            var addResult = task.AddLabel(label.Id);
            if (addResult.IsFailure) return addResult;
        }
        else
        {
            if (task.Labels.All(l => l.LabelId != label.Id)) return Result.Success();
            var removeResult = task.RemoveLabel(label.Id);
            if (removeResult.IsFailure) return removeResult;
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
