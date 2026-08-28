using FlowForge.Application.AI.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.AI.Queries;

public record SuggestTaskBreakdownQuery(Guid TaskId) : IRequest<Result<TaskBreakdownSuggestionDto>>;

public class SuggestTaskBreakdownQueryHandler : IRequestHandler<SuggestTaskBreakdownQuery, Result<TaskBreakdownSuggestionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAiService _ai;

    public SuggestTaskBreakdownQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IAiService ai)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ai = ai;
    }

    public async Task<Result<TaskBreakdownSuggestionDto>> Handle(SuggestTaskBreakdownQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<TaskBreakdownSuggestionDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<TaskBreakdownSuggestionDto>(Error.NotFound("Task.NotFound", "Task not found"));

        var result = await _ai.SuggestSubtasksAsync(task.Title, task.Description, ct);
        if (result.IsFailure) return Result.Failure<TaskBreakdownSuggestionDto>(result.Error);

        return Result.Success(new TaskBreakdownSuggestionDto(result.Value.Titles));
    }
}
