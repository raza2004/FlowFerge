namespace FlowForge.Domain.Features.Repositories;

public interface IFeatureFlagRepository
{
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken ct = default);
    Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task AddAsync(FeatureFlag flag, CancellationToken ct = default);
}
