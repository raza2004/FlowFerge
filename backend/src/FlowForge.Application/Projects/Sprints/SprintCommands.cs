using FlowForge.Application.Features;
using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Sprints;

public record CreateSprintCommand(Guid ProjectId, string Name, string? Goal, DateTime StartDate, DateTime EndDate)
    : IRequest<Result<SprintDto>>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.Sprints;
}
public record UpdateSprintCommand(Guid SprintId, string Name, string? Goal, DateTime StartDate, DateTime EndDate) : IRequest<Result>;
public record StartSprintCommand(Guid SprintId) : IRequest<Result>;

/// <summary>Finishes the sprint. Unfinished tasks go to <paramref name="MoveIncompleteToSprintId"/>, or back to the backlog when null.</summary>
public record CompleteSprintCommand(Guid SprintId, string? RetrospectiveNotes, Guid? MoveIncompleteToSprintId) : IRequest<Result>;

/// <summary>Cancels the sprint and returns all its tasks to the backlog.</summary>
public record CancelSprintCommand(Guid SprintId) : IRequest<Result>;
public record UpdateRetrospectiveCommand(Guid SprintId, string? Notes) : IRequest<Result>;
public record AssignTaskToSprintCommand(Guid TaskId, Guid? SprintId) : IRequest<Result>;

public class CreateSprintCommandValidator : AbstractValidator<CreateSprintCommand>
{
    public CreateSprintCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Goal).MaximumLength(2000);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");
    }
}

public class UpdateSprintCommandValidator : AbstractValidator<UpdateSprintCommand>
{
    public UpdateSprintCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Goal).MaximumLength(2000);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");
    }
}

public class SprintCommandHandlers :
    IRequestHandler<CreateSprintCommand, Result<SprintDto>>,
    IRequestHandler<UpdateSprintCommand, Result>,
    IRequestHandler<StartSprintCommand, Result>,
    IRequestHandler<CompleteSprintCommand, Result>,
    IRequestHandler<CancelSprintCommand, Result>,
    IRequestHandler<UpdateRetrospectiveCommand, Result>,
    IRequestHandler<AssignTaskToSprintCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public SprintCommandHandlers(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    private bool HasTenant => _currentUser.TenantId != null && _currentUser.UserId != null;
    private static Error NoTenant => Error.Unauthorized("Auth.NoTenant", "No active tenant");
    private static Error SprintNotFound => Error.NotFound("Sprint.NotFound", "Sprint not found");

    private async Task<Sprint?> OwnSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await _uow.Sprints.GetByIdAsync(sprintId, ct);
        return sprint != null && sprint.TenantId == _currentUser.TenantId ? sprint : null;
    }

    public async Task<Result<SprintDto>> Handle(CreateSprintCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure<SprintDto>(NoTenant);

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId!.Value)
            return Result.Failure<SprintDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var sprintResult = Sprint.Create(project.TenantId, project.Id, request.Name,
            AsUtcDate(request.StartDate), AsUtcDate(request.EndDate), _currentUser.UserId!.Value, request.Goal?.Trim());
        if (sprintResult.IsFailure) return Result.Failure<SprintDto>(sprintResult.Error);

        await _uow.Sprints.AddAsync(sprintResult.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(SprintMapper.ToDto(sprintResult.Value, Array.Empty<ProjectTask>()));
    }

    public async Task<Result> Handle(UpdateSprintCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure(NoTenant);
        var sprint = await OwnSprintAsync(request.SprintId, ct);
        if (sprint == null) return Result.Failure(SprintNotFound);

        var result = sprint.UpdateDetails(request.Name, request.Goal, AsUtcDate(request.StartDate), AsUtcDate(request.EndDate));
        if (result.IsFailure) return result;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(StartSprintCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure(NoTenant);
        var sprint = await OwnSprintAsync(request.SprintId, ct);
        if (sprint == null) return Result.Failure(SprintNotFound);

        var active = await _uow.Sprints.GetActiveSprintAsync(sprint.ProjectId, ct);
        if (active != null && active.Id != sprint.Id)
            return Result.Failure(Error.Conflict("Sprint.AlreadyActive", $"\"{active.Name}\" is still active. Complete it before starting another sprint."));

        var result = sprint.Start();
        if (result.IsFailure) return result;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(CompleteSprintCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure(NoTenant);
        var sprint = await OwnSprintAsync(request.SprintId, ct);
        if (sprint == null) return Result.Failure(SprintNotFound);
        if (sprint.Status != SprintStatus.Active)
            return Result.Failure(Error.Conflict("Sprint.CannotComplete", "Only the active sprint can be completed"));

        Sprint? target = null;
        if (request.MoveIncompleteToSprintId.HasValue)
        {
            target = await OwnSprintAsync(request.MoveIncompleteToSprintId.Value, ct);
            if (target == null || target.ProjectId != sprint.ProjectId || target.Status != SprintStatus.Planning)
                return Result.Failure(Error.Validation("Sprint.InvalidTarget", "Unfinished tasks can only move to a planned sprint in this project"));
        }

        var complete = sprint.Complete(request.RetrospectiveNotes);
        if (complete.IsFailure) return complete;

        foreach (var task in (await _uow.Tasks.GetBySprintAsync(sprint.Id, ct)).Where(t => !t.IsCompleted))
            task.AssignToSprint(target?.Id);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(CancelSprintCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure(NoTenant);
        var sprint = await OwnSprintAsync(request.SprintId, ct);
        if (sprint == null) return Result.Failure(SprintNotFound);

        var result = sprint.Cancel();
        if (result.IsFailure) return result;

        foreach (var task in await _uow.Tasks.GetBySprintAsync(sprint.Id, ct))
            task.AssignToSprint(null);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(UpdateRetrospectiveCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure(NoTenant);
        var sprint = await OwnSprintAsync(request.SprintId, ct);
        if (sprint == null) return Result.Failure(SprintNotFound);

        var result = sprint.UpdateRetrospective(request.Notes);
        if (result.IsFailure) return result;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(AssignTaskToSprintCommand request, CancellationToken ct)
    {
        if (!HasTenant) return Result.Failure(NoTenant);

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId!.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        if (request.SprintId.HasValue)
        {
            var sprint = await OwnSprintAsync(request.SprintId.Value, ct);
            if (sprint == null || sprint.ProjectId != task.ProjectId)
                return Result.Failure(Error.NotFound("Sprint.NotFound", "Sprint not found in this task's project"));
            if (!sprint.IsOpen)
                return Result.Failure(Error.Conflict("Sprint.Closed", "That sprint is already finished"));
        }

        // A task already in a finished sprint stays there as history - only open sprints can be left.
        if (task.SprintId.HasValue && task.SprintId != request.SprintId)
        {
            var current = await _uow.Sprints.GetByIdAsync(task.SprintId.Value, ct);
            if (current is { IsOpen: false })
                return Result.Failure(Error.Conflict("Sprint.Closed", "This task belongs to a finished sprint"));
        }

        var result = task.AssignToSprint(request.SprintId);
        if (result.IsFailure) return result;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Sprints run on whole UTC days; strips any time-of-day the client sent.</summary>
    private static DateTime AsUtcDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
