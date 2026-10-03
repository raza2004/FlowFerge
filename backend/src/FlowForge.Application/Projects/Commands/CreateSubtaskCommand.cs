using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

/// <summary>
/// Creates a subtask in the same list as its parent, inheriting type and priority -
/// the same placement ApplyTaskBreakdownCommand uses for AI-suggested subtasks.
/// </summary>
public record CreateSubtaskCommand(Guid ParentTaskId, string Title) : IRequest<Result<SubtaskDto>>;

public class CreateSubtaskCommandValidator : AbstractValidator<CreateSubtaskCommand>
{
    public CreateSubtaskCommandValidator()
    {
        RuleFor(x => x.ParentTaskId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
    }
}

public class CreateSubtaskCommandHandler : IRequestHandler<CreateSubtaskCommand, Result<SubtaskDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateSubtaskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<SubtaskDto>> Handle(CreateSubtaskCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<SubtaskDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var parent = await _uow.Tasks.GetByIdAsync(request.ParentTaskId, ct);
        if (parent == null || parent.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<SubtaskDto>(Error.NotFound("Task.NotFound", "Task not found"));

        if (parent.ParentTaskId.HasValue)
            return Result.Failure<SubtaskDto>(Error.Validation("Task.NestedSubtask", "Subtasks can't have their own subtasks"));

        var project = await _uow.Projects.GetByIdAsync(parent.ProjectId, ct);
        if (project == null)
            return Result.Failure<SubtaskDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var taskNumber = await _uow.Tasks.GetNextTaskNumberAsync(parent.ProjectId, ct);
        var subtaskResult = ProjectTask.Create(
            _currentUser.TenantId.Value, parent.ProjectId, parent.BoardId, parent.ListId,
            $"{project.Key.Value}-{taskNumber}", request.Title, parent.Type, parent.Priority,
            _currentUser.UserId.Value, parentTaskId: parent.Id);

        if (subtaskResult.IsFailure) return Result.Failure<SubtaskDto>(subtaskResult.Error);

        var subtask = subtaskResult.Value;
        await _uow.Tasks.AddAsync(subtask, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new SubtaskDto(subtask.Id, subtask.TaskNumber, subtask.Title, subtask.IsCompleted));
    }
}
