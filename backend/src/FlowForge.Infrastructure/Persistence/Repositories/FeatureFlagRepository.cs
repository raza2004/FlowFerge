using FlowForge.Domain.Features;
using FlowForge.Domain.Features.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly FlowForgeDbContext _ctx;
    public FeatureFlagRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default) =>
        await _ctx.FeatureFlags.Include(f => f.Overrides).OrderBy(f => f.Name).ToListAsync(ct);

    public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        _ctx.FeatureFlags.Include(f => f.Overrides).FirstOrDefaultAsync(f => f.Key == key, ct);

    public async Task AddAsync(FeatureFlag flag, CancellationToken ct = default) =>
        await _ctx.FeatureFlags.AddAsync(flag, ct);
}
