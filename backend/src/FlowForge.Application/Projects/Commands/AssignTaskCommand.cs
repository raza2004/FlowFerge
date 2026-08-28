using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record AssignTaskCommand(Guid TaskId, Guid AssigneeId) : IRequest<Result>;

public class AssignTaskCommandValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.AssigneeId).NotEmpty();
    }
}

public class AssignTaskCommandHandler : IRequestHandler<AssignTaskCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AssignTaskCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AssignTaskCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Task.NotFound", "Task not found"));

        var assignResult = task.Assign(request.AssigneeId, _currentUser.UserId.Value, _currentUser.TenantId.Value);
        if (assignResult.IsFailure) return assignResult;

        _uow.Tasks.Update(task);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
