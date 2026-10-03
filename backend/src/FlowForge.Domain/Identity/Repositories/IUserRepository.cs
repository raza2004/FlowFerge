using FlowForge.Domain.Identity.ValueObjects;

namespace FlowForge.Domain.Identity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default);
    Task<bool> AnyExistAsync(CancellationToken ct = default);
    Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Looks users up by id regardless of membership, so removed members still show their name on old work.</summary>
    Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IEnumerable<User>> GetAllAsync(int take = 200, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    void Update(User user);
}
