using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class MembershipRepository : IMembershipRepository
{
    private readonly FlowForgeDbContext _ctx;
    public MembershipRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Memberships.Include(m => m.User).Include(m => m.Tenant).FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Membership?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken ct = default) =>
        _ctx.Memberships.FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, ct);

    public Task<Membership?> GetByInvitationTokenAsync(string token, CancellationToken ct = default) =>
        _ctx.Memberships.FirstOrDefaultAsync(m => m.InvitationToken == token, ct);

    public async Task<IEnumerable<Membership>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await _ctx.Memberships.Include(m => m.User).Where(m => m.TenantId == tenantId).ToListAsync(ct);

    public async Task<IEnumerable<Membership>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _ctx.Memberships.Include(m => m.Tenant).Where(m => m.UserId == userId).ToListAsync(ct);

    public async Task AddAsync(Membership membership, CancellationToken ct = default) =>
        await _ctx.Memberships.AddAsync(membership, ct);

    public void Update(Membership membership) => _ctx.Memberships.Update(membership);
    public void Delete(Membership membership) => _ctx.Memberships.Remove(membership);
}
