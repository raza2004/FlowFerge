using FlowForge.Application.AI.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.AI.Queries;

public record SuggestAssigneeQuery(Guid TaskId) : IRequest<Result<AssigneeSuggestionDto>>;

public class SuggestAssigneeQueryHandler : IRequestHandler<SuggestAssigneeQuery, Result<AssigneeSuggestionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAiService _ai;

    public SuggestAssigneeQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IAiService ai)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ai = ai;
    }

    public async Task<Result<AssigneeSuggestionDto>> Handle(SuggestAssigneeQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<AssigneeSuggestionDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<AssigneeSuggestionDto>(Error.NotFound("Task.NotFound", "Task not found"));

        var project = await _uow.Projects.GetByIdWithDetailsAsync(task.ProjectId, ct);
        if (project == null)
            return Result.Failure<AssigneeSuggestionDto>(Error.NotFound("Project.NotFound", "Project not found"));

        if (project.Members.Count == 0)
            return Result.Failure<AssigneeSuggestionDto>(Error.Validation("Project.NoMembers", "This project has no members to assign to"));

        var projectTasks = (await _uow.Tasks.GetByProjectAsync(task.ProjectId, ct)).ToList();

        var candidates = new List<AssigneeCandidate>();
        var namesByUserId = new Dictionary<Guid, string>();
        foreach (var member in project.Members)
        {
            var user = await _uow.Users.GetByIdAsync(member.UserId, ct);
            if (user == null) continue;

            var openCount = projectTasks.Count(t => t.AssigneeId == member.UserId && !t.IsCompleted);
            candidates.Add(new AssigneeCandidate(member.UserId, user.FullName, openCount));
            namesByUserId[member.UserId] = user.FullName;
        }

        var suggestion = await _ai.SuggestAssigneeAsync(task.Title, task.Description, candidates, ct);
        if (suggestion.IsFailure) return Result.Failure<AssigneeSuggestionDto>(suggestion.Error);

        if (!namesByUserId.TryGetValue(suggestion.Value.SuggestedUserId, out var name))
            return Result.Failure<AssigneeSuggestionDto>(Error.Failure("AI.InvalidSuggestion", "AI suggested a user outside this project"));

        return Result.Success(new AssigneeSuggestionDto(suggestion.Value.SuggestedUserId, name, suggestion.Value.Reasoning));
    }
}
