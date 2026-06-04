namespace FlowForge.Domain.Identity.Repositories;

public interface IMembershipRepository
{
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Membership?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<Membership?> GetByInvitationTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<Membership>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<Membership>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Membership membership, CancellationToken ct = default);
    void Update(Membership membership);
    void Delete(Membership membership);
}
