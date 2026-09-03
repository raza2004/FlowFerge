namespace FlowForge.Domain.Auditing;

public interface IAuditLogRepository
{
    Task<IEnumerable<AuditLog>> GetRecentAsync(int take = 100, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByTenantAsync(Guid tenantId, int take = 100, CancellationToken ct = default);
    Task AddAsync(AuditLog log, CancellationToken ct = default);
}
