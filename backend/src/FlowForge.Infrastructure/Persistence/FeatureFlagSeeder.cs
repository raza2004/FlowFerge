using FlowForge.Application.Features;
using FlowForge.Domain.Features;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence;

public static class FeatureFlagSeeder
{
    /// <summary>
    /// Creates a flag row (switched on) for every feature in FeatureKeys that doesn't have one,
    /// so each shows up in the admin panel. Existing rows are never touched, so an admin's
    /// choices survive restarts and deployments.
    /// </summary>
    public static async Task EnsureAsync(FlowForgeDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FeatureFlags.Select(f => f.Key).ToListAsync(ct);

        foreach (var definition in FeatureKeys.All.Where(d => !existing.Contains(d.Key)))
        {
            var flag = FeatureFlag.Create(definition.Key, definition.Name, definition.Description, isEnabled: true);
            if (flag.IsSuccess) db.FeatureFlags.Add(flag.Value);
        }

        await db.SaveChangesAsync(ct);
    }
}
