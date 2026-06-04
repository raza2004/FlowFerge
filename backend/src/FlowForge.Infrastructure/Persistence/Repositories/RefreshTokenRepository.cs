using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly FlowForgeDbContext _ctx;
    public RefreshTokenRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        _ctx.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task<IEnumerable<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _ctx.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default) =>
        await _ctx.RefreshTokens.AddAsync(refreshToken, ct);

    public void Update(RefreshToken refreshToken) => _ctx.RefreshTokens.Update(refreshToken);
}
