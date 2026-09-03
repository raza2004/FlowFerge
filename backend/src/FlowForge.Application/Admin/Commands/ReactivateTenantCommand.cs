using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Commands;

public record ReactivateTenantCommand(Guid TenantId) : IRequest<Result>;

public class ReactivateTenantCommandHandler : IRequestHandler<ReactivateTenantCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ReactivateTenantCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ReactivateTenantCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var tenant = await _uow.Tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant == null)
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Tenant not found"));

        var result = tenant.Reactivate();
        if (result.IsFailure) return result;

        _uow.Tenants.Update(tenant);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
