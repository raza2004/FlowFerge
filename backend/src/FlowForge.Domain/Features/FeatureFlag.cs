using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Features;

/// <summary>
/// A system-wide switch for a product feature, managed by system admins. A workspace can have an
/// override that wins over the global setting (to trial something on one workspace, or to switch
/// it off for just one).
/// </summary>
public sealed class FeatureFlag : Entity
{
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    private readonly List<FeatureFlagOverride> _overrides = new();
    public IReadOnlyCollection<FeatureFlagOverride> Overrides => _overrides.AsReadOnly();

    private FeatureFlag() { }

    public static Result<FeatureFlag> Create(string key, string name, string description, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            return Result.Failure<FeatureFlag>(Error.Validation("FeatureFlag.InvalidKey", "Key must be 1-100 chars"));

        return Result.Success(new FeatureFlag
        {
            Key = key.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description.Trim(),
            IsEnabled = isEnabled
        });
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Touch();
    }

    public void SetOverride(Guid tenantId, bool enabled)
    {
        var existing = _overrides.FirstOrDefault(o => o.TenantId == tenantId);
        if (existing != null) existing.IsEnabled = enabled;
        else _overrides.Add(new FeatureFlagOverride { FeatureFlagId = Id, TenantId = tenantId, IsEnabled = enabled });
        Touch();
    }

    public void RemoveOverride(Guid tenantId)
    {
        var existing = _overrides.FirstOrDefault(o => o.TenantId == tenantId);
        if (existing == null) return;
        _overrides.Remove(existing);
        Touch();
    }

    /// <summary>The workspace's override if it has one, otherwise the global setting.</summary>
    public bool IsEnabledFor(Guid tenantId) =>
        _overrides.FirstOrDefault(o => o.TenantId == tenantId)?.IsEnabled ?? IsEnabled;
}

public sealed class FeatureFlagOverride
{
    public Guid FeatureFlagId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsEnabled { get; set; }
}
