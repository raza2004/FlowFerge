using FlowForge.Domain.Identity.ValueObjects;

namespace FlowForge.Domain.Identity.Repositories;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<Invitation>> GetPendingByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<Invitation>> GetPendingByTenantAndEmailAsync(Guid tenantId, Email email, CancellationToken ct = default);
    Task AddAsync(Invitation invitation, CancellationToken ct = default);
}
