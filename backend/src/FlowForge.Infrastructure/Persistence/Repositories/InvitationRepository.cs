using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class InvitationRepository : IInvitationRepository
{
    private readonly FlowForgeDbContext _ctx;
    public InvitationRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    private IQueryable<Invitation> Pending =>
        _ctx.Invitations.Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow);

    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Invitations.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        _ctx.Invitations.FirstOrDefaultAsync(i => i.Token == token, ct);

    public async Task<IEnumerable<Invitation>> GetPendingByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await Pending.Where(i => i.TenantId == tenantId).OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<Invitation>> GetPendingByTenantAndEmailAsync(Guid tenantId, Email email, CancellationToken ct = default) =>
        await Pending.Where(i => i.TenantId == tenantId && i.Email == email).ToListAsync(ct);

    public async Task AddAsync(Invitation invitation, CancellationToken ct = default) =>
        await _ctx.Invitations.AddAsync(invitation, ct);
}
