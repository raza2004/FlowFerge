using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record UpdateSlackWebhookCommand(string? WebhookUrl) : IRequest<Result>;

public class UpdateSlackWebhookCommandHandler : IRequestHandler<UpdateSlackWebhookCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateSlackWebhookCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateSlackWebhookCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var membership = await _uow.Memberships.GetByUserAndTenantAsync(_currentUser.UserId.Value, _currentUser.TenantId.Value, ct);
        if (membership == null || (membership.Role != MembershipRole.Owner && membership.Role != MembershipRole.Admin))
            return Result.Failure(Error.Forbidden("Auth.InsufficientRole", "Only workspace owners and admins can change the Slack webhook"));

        var tenant = await _uow.Tenants.GetByIdAsync(_currentUser.TenantId.Value, ct);
        if (tenant == null)
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Tenant not found"));

        var result = tenant.SetSlackWebhook(request.WebhookUrl);
        if (result.IsFailure) return result;

        _uow.Tenants.Update(tenant);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
