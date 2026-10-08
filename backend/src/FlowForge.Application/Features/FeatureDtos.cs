namespace FlowForge.Application.Features;

public record FeatureOverrideDto(Guid TenantId, string TenantName, bool IsEnabled);

public record FeatureFlagDto(string Key, string Name, string Description, bool IsEnabled, List<FeatureOverrideDto> Overrides);

public record SetFeatureFlagRequest(bool Enabled);

/// <param name="Enabled">true/false forces the feature on/off for the workspace; null removes the override.</param>
public record SetFeatureOverrideRequest(bool? Enabled);
