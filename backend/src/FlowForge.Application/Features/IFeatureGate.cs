using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;

namespace FlowForge.Application.Features;

/// <summary>Marks a request as belonging to a switchable feature; the pipeline blocks it while the feature is off.</summary>
public interface IRequiresFeature
{
    string FeatureKey { get; }
}

public interface IFeatureGate
{
    /// <summary>Whether the feature is on for this workspace. A feature with no flag row counts as on.</summary>
    Task<bool> IsEnabledAsync(string featureKey, Guid tenantId, CancellationToken ct = default);

    Task<Dictionary<string, bool>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Call after any flag or override changes so cached answers for every workspace are dropped.</summary>
    Task InvalidateAsync(CancellationToken ct = default);
}

/// <summary>
/// Evaluates flags, caching each workspace's full on/off map briefly (every gated request asks).
/// Cache keys embed a version number, and an admin change bumps it, so one increment invalidates
/// every workspace's entry without having to list them. Entries also expire on their own, which
/// bounds how stale an answer can be even if the cache was unreachable during an invalidation.
/// </summary>
public class FeatureGate : IFeatureGate
{
    private const string VersionKey = "features:version";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IUnitOfWork _uow;
    private readonly ICache _cache;

    public FeatureGate(IUnitOfWork uow, ICache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string featureKey, Guid tenantId, CancellationToken ct = default)
    {
        var all = await GetAllForTenantAsync(tenantId, ct);
        return !all.TryGetValue(featureKey, out var enabled) || enabled;
    }

    public async Task<Dictionary<string, bool>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var version = await _cache.GetAsync<long>(VersionKey, ct);
        var cacheKey = $"features:{version}:{tenantId}";

        var cached = await _cache.GetAsync<Dictionary<string, bool>>(cacheKey, ct);
        if (cached != null) return cached;

        var flags = (await _uow.FeatureFlags.GetAllAsync(ct)).ToDictionary(f => f.Key);
        var map = FeatureKeys.All.ToDictionary(
            d => d.Key,
            d => !flags.TryGetValue(d.Key, out var flag) || flag.IsEnabledFor(tenantId));

        await _cache.SetAsync(cacheKey, map, Ttl, ct);
        return map;
    }

    public async Task InvalidateAsync(CancellationToken ct = default) =>
        await _cache.IncrementAsync(VersionKey, ct);
}
