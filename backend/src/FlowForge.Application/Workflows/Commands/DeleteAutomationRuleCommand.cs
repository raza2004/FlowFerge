using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Workflows.Commands;

public record DeleteAutomationRuleCommand(Guid RuleId) : IRequest<Result>;

public class DeleteAutomationRuleCommandHandler : IRequestHandler<DeleteAutomationRuleCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteAutomationRuleCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteAutomationRuleCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var rule = await _uow.AutomationRules.GetByIdAsync(request.RuleId, ct);
        if (rule == null || rule.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Automation.NotFound", "Automation rule not found"));

        rule.SoftDelete(_currentUser.UserId.Value);
        _uow.AutomationRules.Update(rule);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
