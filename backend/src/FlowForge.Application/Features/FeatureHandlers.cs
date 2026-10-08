using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Features;

public record GetFeatureFlagsQuery : IRequest<Result<List<FeatureFlagDto>>>;
public record SetFeatureFlagCommand(string Key, bool Enabled) : IRequest<Result>;
public record SetFeatureOverrideCommand(string Key, Guid TenantId, bool? Enabled) : IRequest<Result>;

/// <summary>What the signed-in user's workspace can use right now; the frontend hides controls for anything off.</summary>
public record GetMyFeaturesQuery : IRequest<Result<Dictionary<string, bool>>>;

public class FeatureHandlers :
    IRequestHandler<GetFeatureFlagsQuery, Result<List<FeatureFlagDto>>>,
    IRequestHandler<SetFeatureFlagCommand, Result>,
    IRequestHandler<SetFeatureOverrideCommand, Result>,
    IRequestHandler<GetMyFeaturesQuery, Result<Dictionary<string, bool>>>
{
    private static readonly Error AdminOnly = Error.Forbidden("Admin.Forbidden", "System admin access required");

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IFeatureGate _gate;

    public FeatureHandlers(IUnitOfWork uow, ICurrentUser currentUser, IFeatureGate gate)
    {
        _uow = uow;
        _currentUser = currentUser;
        _gate = gate;
    }

    public async Task<Result<List<FeatureFlagDto>>> Handle(GetFeatureFlagsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin) return Result.Failure<List<FeatureFlagDto>>(AdminOnly);

        var flags = await _uow.FeatureFlags.GetAllAsync(ct);
        var tenants = (await _uow.Tenants.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Name);

        return Result.Success(flags.Select(f => new FeatureFlagDto(
            f.Key, f.Name, f.Description, f.IsEnabled,
            f.Overrides
                .Select(o => new FeatureOverrideDto(o.TenantId, tenants.GetValueOrDefault(o.TenantId, "Deleted workspace"), o.IsEnabled))
                .OrderBy(o => o.TenantName)
                .ToList())).ToList());
    }

    public async Task<Result> Handle(SetFeatureFlagCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin) return Result.Failure(AdminOnly);

        var flag = await _uow.FeatureFlags.GetByKeyAsync(request.Key, ct);
        if (flag == null) return Result.Failure(Error.NotFound("FeatureFlag.NotFound", "Feature not found"));

        flag.SetEnabled(request.Enabled);
        await _uow.SaveChangesAsync(ct);
        await _gate.InvalidateAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(SetFeatureOverrideCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsSystemAdmin) return Result.Failure(AdminOnly);

        var flag = await _uow.FeatureFlags.GetByKeyAsync(request.Key, ct);
        if (flag == null) return Result.Failure(Error.NotFound("FeatureFlag.NotFound", "Feature not found"));

        if (await _uow.Tenants.GetByIdAsync(request.TenantId, ct) == null)
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Workspace not found"));

        if (request.Enabled.HasValue) flag.SetOverride(request.TenantId, request.Enabled.Value);
        else flag.RemoveOverride(request.TenantId);

        await _uow.SaveChangesAsync(ct);
        await _gate.InvalidateAsync(ct);
        return Result.Success();
    }

    public async Task<Result<Dictionary<string, bool>>> Handle(GetMyFeaturesQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<Dictionary<string, bool>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        return Result.Success(await _gate.GetAllForTenantAsync(_currentUser.TenantId.Value, ct));
    }
}
