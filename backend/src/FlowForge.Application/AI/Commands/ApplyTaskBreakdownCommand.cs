using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.AI.Commands;

/// <summary>
/// Turns AI-suggested subtask titles (which the user has reviewed and can edit/remove
/// in the UI before calling this) into real ProjectTask rows under the parent task -
/// this is what makes the "AI as a feature" claim literal instead of decorative text.
/// </summary>
public record ApplyTaskBreakdownCommand(Guid ParentTaskId, List<string> SubtaskTitles) : IRequest<Result<int>>;

public class ApplyTaskBreakdownCommandValidator : AbstractValidator<ApplyTaskBreakdownCommand>
{
    public ApplyTaskBreakdownCommandValidator()
    {
        RuleFor(x => x.ParentTaskId).NotEmpty();
        RuleFor(x => x.SubtaskTitles).NotEmpty();
        RuleForEach(x => x.SubtaskTitles).NotEmpty().MaximumLength(500);
    }
}

public class ApplyTaskBreakdownCommandHandler : IRequestHandler<ApplyTaskBreakdownCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ApplyTaskBreakdownCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(ApplyTaskBreakdownCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<int>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var parent = await _uow.Tasks.GetByIdAsync(request.ParentTaskId, ct);
        if (parent == null || parent.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<int>(Error.NotFound("Task.NotFound", "Task not found"));

        var project = await _uow.Projects.GetByIdAsync(parent.ProjectId, ct);
        if (project == null)
            return Result.Failure<int>(Error.NotFound("Project.NotFound", "Project not found"));

        var created = 0;
        foreach (var title in request.SubtaskTitles)
        {
            var taskNumber = await _uow.Tasks.GetNextTaskNumberAsync(parent.ProjectId, ct);
            var taskResult = ProjectTask.Create(
                _currentUser.TenantId.Value, parent.ProjectId, parent.BoardId, parent.ListId,
                $"{project.Key.Value}-{taskNumber}", title, parent.Type, parent.Priority,
                _currentUser.UserId.Value, parentTaskId: parent.Id);

            if (taskResult.IsFailure) continue;

            await _uow.Tasks.AddAsync(taskResult.Value, ct);
            created++;
        }

        if (created > 0) await _uow.SaveChangesAsync(ct);
        return Result.Success(created);
    }
}
