using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record CreateTaskCommand(
    Guid ProjectId,
    Guid BoardId,
    Guid ListId,
    string Title,
    string? Description,
    TaskType Type,
    TaskPriority Priority
) : IRequest<Result<TaskCardDto>>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.ListId).NotEmpty();
    }
}

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Result<TaskCardDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateTaskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TaskCardDto>> Handle(CreateTaskCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<TaskCardDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<TaskCardDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var taskNumber = await _uow.Tasks.GetNextTaskNumberAsync(request.ProjectId, ct);
        var taskNumberStr = $"{project.Key.Value}-{taskNumber}";

        var taskResult = ProjectTask.Create(
            _currentUser.TenantId.Value,
            request.ProjectId,
            request.BoardId,
            request.ListId,
            taskNumberStr,
            request.Title,
            request.Type,
            request.Priority,
            _currentUser.UserId.Value);

        if (taskResult.IsFailure) return Result.Failure<TaskCardDto>(taskResult.Error);

        var task = taskResult.Value;
        if (!string.IsNullOrWhiteSpace(request.Description))
            task.UpdateDetails(task.Title, request.Description, task.Type, task.Priority);

        await _uow.Tasks.AddAsync(task, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new TaskCardDto(
            task.Id, task.TaskNumber, task.Title,
            task.Type.ToString(), task.Priority.ToString(),
            task.AssigneeId, null,
            task.DueDate, task.IsOverdue,
            task.Position, task.CommentCount
        ));
    }
}
