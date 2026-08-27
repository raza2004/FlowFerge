using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Workflows.Commands;

public record ToggleAutomationRuleCommand(Guid RuleId, bool Enabled) : IRequest<Result>;

public class ToggleAutomationRuleCommandHandler : IRequestHandler<ToggleAutomationRuleCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ToggleAutomationRuleCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ToggleAutomationRuleCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var rule = await _uow.AutomationRules.GetByIdAsync(request.RuleId, ct);
        if (rule == null || rule.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Automation.NotFound", "Automation rule not found"));

        if (request.Enabled) rule.Enable(); else rule.Disable();

        _uow.AutomationRules.Update(rule);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
