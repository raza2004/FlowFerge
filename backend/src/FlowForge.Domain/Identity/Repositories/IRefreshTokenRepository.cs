namespace FlowForge.Domain.Identity.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Permanently deletes tokens that have been expired or revoked since before <paramref name="olderThan"/>; returns how many.</summary>
    Task<int> DeleteInactiveAsync(DateTime olderThan, CancellationToken ct = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
    void Update(RefreshToken refreshToken);
}
