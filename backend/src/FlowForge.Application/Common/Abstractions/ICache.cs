namespace FlowForge.Application.Common.Abstractions;

/// <summary>
/// A best-effort cache (Redis in Infrastructure). It can only ever make things faster, never break
/// them: if the cache is down or slow, reads behave as a miss and writes are dropped, so callers
/// always fall back to the database and must not depend on a value being there.
/// </summary>
public interface ICache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Atomically adds one to a counter and returns the new value (0 if the cache is unavailable).</summary>
    Task<long> IncrementAsync(string key, CancellationToken ct = default);
}
