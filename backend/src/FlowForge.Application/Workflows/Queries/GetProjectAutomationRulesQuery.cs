using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Workflows.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Workflows.Queries;

public record GetProjectAutomationRulesQuery(Guid ProjectId) : IRequest<Result<List<AutomationRuleDto>>>;

public class GetProjectAutomationRulesQueryHandler
    : IRequestHandler<GetProjectAutomationRulesQuery, Result<List<AutomationRuleDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProjectAutomationRulesQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AutomationRuleDto>>> Handle(GetProjectAutomationRulesQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<AutomationRuleDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdWithDetailsAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<List<AutomationRuleDto>>(Error.NotFound("Project.NotFound", "Project not found"));

        var lists = project.Boards.SelectMany(b => b.Lists).ToDictionary(l => l.Id);
        var rules = await _uow.AutomationRules.GetByProjectAsync(request.ProjectId, ct);
        var userIds = rules.Select(r => r.ActionUserId).Distinct().ToList();

        var users = new Dictionary<Guid, string>();
        foreach (var userId in userIds)
        {
            var user = await _uow.Users.GetByIdAsync(userId, ct);
            if (user != null) users[userId] = user.FullName;
        }

        var result = rules
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AutomationRuleDto(
                r.Id, r.Name, r.TriggerType, r.TriggerListId,
                lists.TryGetValue(r.TriggerListId, out var l) ? l.Name : "Unknown list",
                r.ActionType, r.ActionUserId,
                users.TryGetValue(r.ActionUserId, out var name) ? name : "Unknown user",
                r.IsEnabled, r.CreatedAt))
            .ToList();

        return Result.Success(result);
    }
}
