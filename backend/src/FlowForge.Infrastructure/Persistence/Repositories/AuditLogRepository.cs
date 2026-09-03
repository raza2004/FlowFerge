using FlowForge.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly FlowForgeDbContext _ctx;
    public AuditLogRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int take = 100, CancellationToken ct = default) =>
        await _ctx.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(take).ToListAsync(ct);

    public async Task<IEnumerable<AuditLog>> GetByTenantAsync(Guid tenantId, int take = 100, CancellationToken ct = default) =>
        await _ctx.AuditLogs.Where(a => a.TenantId == tenantId).OrderByDescending(a => a.CreatedAt).Take(take).ToListAsync(ct);

    public async Task AddAsync(AuditLog log, CancellationToken ct = default) =>
        await _ctx.AuditLogs.AddAsync(log, ct);
}
