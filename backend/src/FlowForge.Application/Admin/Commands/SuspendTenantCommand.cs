using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Admin.Commands;

public record SuspendTenantCommand(Guid TenantId, string Reason) : IRequest<Result>;

public class SuspendTenantCommandValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class SuspendTenantCommandHandler : IRequestHandler<SuspendTenantCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public SuspendTenantCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SuspendTenantCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin)
            return Result.Failure(Error.Forbidden("Admin.Forbidden", "System admin access required"));

        var tenant = await _uow.Tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant == null)
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Tenant not found"));

        var result = tenant.Suspend(request.Reason);
        if (result.IsFailure) return result;

        _uow.Tenants.Update(tenant);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
