using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record UpdateTaskCommand(
    Guid TaskId,
    string Title,
    string? Description,
    TaskType Type,
    TaskPriority Priority,
    DateTime? DueDate,
    double? EstimatedHours,
    int? StoryPoints
) : IRequest<Result>;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(20000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0).When(x => x.EstimatedHours.HasValue);
        RuleFor(x => x.StoryPoints).InclusiveBetween(0, 100).When(x => x.StoryPoints.HasValue);
    }
}

public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateTaskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateTaskCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;
        var detailsResult = task.UpdateDetails(request.Title, description, request.Type, request.Priority);
        if (detailsResult.IsFailure) return detailsResult;

        task.SetDueDate(request.DueDate.HasValue ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc) : null);

        var estimateResult = task.SetEstimate(request.EstimatedHours, request.StoryPoints);
        if (estimateResult.IsFailure) return estimateResult;

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
