using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record LogTimeCommand(Guid TaskId, double Hours, DateTime? WorkDate, string? Note) : IRequest<Result<TimeEntryDto>>;

/// <summary>Deletes one of your own time entries and takes its hours back off the task's total.</summary>
public record DeleteTimeEntryCommand(Guid TimeEntryId) : IRequest<Result>;

public class LogTimeCommandValidator : AbstractValidator<LogTimeCommand>
{
    public LogTimeCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(TimeEntry.MaxHoursPerEntry);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.WorkDate).LessThanOrEqualTo(_ => DateTime.UtcNow.Date.AddDays(1))
            .When(x => x.WorkDate.HasValue).WithMessage("You can't log time for a future date");
    }
}

public class TimeEntryCommandHandlers :
    IRequestHandler<LogTimeCommand, Result<TimeEntryDto>>,
    IRequestHandler<DeleteTimeEntryCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public TimeEntryCommandHandlers(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TimeEntryDto>> Handle(LogTimeCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<TimeEntryDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<TimeEntryDto>(Error.NotFound("Task.NotFound", "Task not found"));

        var userId = _currentUser.UserId.Value;
        var entryResult = TimeEntry.Create(task.TenantId, task.Id, userId, request.Hours,
            request.WorkDate ?? DateTime.UtcNow, request.Note);
        if (entryResult.IsFailure) return Result.Failure<TimeEntryDto>(entryResult.Error);

        var entry = entryResult.Value;
        var logResult = task.LogTime(entry.Hours);
        if (logResult.IsFailure) return Result.Failure<TimeEntryDto>(logResult.Error);

        await _uow.Tasks.AddTimeEntryAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        return Result.Success(new TimeEntryDto(entry.Id, userId, user?.FullName ?? "You", entry.Hours, entry.WorkDate, entry.Note, entry.CreatedAt));
    }

    public async Task<Result> Handle(DeleteTimeEntryCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var entry = await _uow.Tasks.GetTimeEntryByIdAsync(request.TimeEntryId, ct);
        if (entry == null || entry.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("TimeEntry.NotFound", "Time entry not found"));

        if (entry.UserId != _currentUser.UserId.Value)
            return Result.Failure(Error.Forbidden("TimeEntry.NotOwner", "You can only delete time you logged yourself"));

        var task = await _uow.Tasks.GetByIdAsync(entry.TaskId, ct);
        task?.RemoveLoggedTime(entry.Hours);
        _uow.Tasks.RemoveTimeEntry(entry);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
