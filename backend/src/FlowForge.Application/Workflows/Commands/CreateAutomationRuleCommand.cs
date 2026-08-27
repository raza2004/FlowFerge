using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Workflows.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Workflows;
using FlowForge.Domain.Workflows.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Workflows.Commands;

public record CreateAutomationRuleCommand(
    Guid ProjectId,
    string Name,
    AutomationTriggerType TriggerType,
    Guid TriggerListId,
    AutomationActionType ActionType,
    Guid ActionUserId
) : IRequest<Result<AutomationRuleDto>>;

public class CreateAutomationRuleCommandValidator : AbstractValidator<CreateAutomationRuleCommand>
{
    public CreateAutomationRuleCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TriggerListId).NotEmpty();
        RuleFor(x => x.ActionUserId).NotEmpty();
    }
}

public class CreateAutomationRuleCommandHandler : IRequestHandler<CreateAutomationRuleCommand, Result<AutomationRuleDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateAutomationRuleCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<AutomationRuleDto>> Handle(CreateAutomationRuleCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<AutomationRuleDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdWithDetailsAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<AutomationRuleDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var triggerList = project.Boards.SelectMany(b => b.Lists).FirstOrDefault(l => l.Id == request.TriggerListId);
        if (triggerList == null)
            return Result.Failure<AutomationRuleDto>(Error.NotFound("BoardList.NotFound", "Trigger list not found on this project's board"));

        var actionUser = await _uow.Users.GetByIdAsync(request.ActionUserId, ct);
        if (actionUser == null)
            return Result.Failure<AutomationRuleDto>(Error.NotFound("User.NotFound", "Action target user not found"));

        var ruleResult = AutomationRule.Create(
            _currentUser.TenantId.Value, request.ProjectId, request.Name,
            request.TriggerType, request.TriggerListId,
            request.ActionType, request.ActionUserId,
            _currentUser.UserId.Value);

        if (ruleResult.IsFailure) return Result.Failure<AutomationRuleDto>(ruleResult.Error);

        var rule = ruleResult.Value;
        await _uow.AutomationRules.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new AutomationRuleDto(
            rule.Id, rule.Name, rule.TriggerType, rule.TriggerListId, triggerList.Name,
            rule.ActionType, rule.ActionUserId, actionUser.FullName, rule.IsEnabled, rule.CreatedAt
        ));
    }
}
