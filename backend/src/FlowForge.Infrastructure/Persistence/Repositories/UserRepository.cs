using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly FlowForgeDbContext _ctx;
    public UserRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        _ctx.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default) =>
        _ctx.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct);

    public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) =>
        _ctx.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await _ctx.Users
            .Where(u => _ctx.Memberships.Any(m => m.TenantId == tenantId && m.UserId == u.Id && m.IsActive))
            .ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _ctx.Users.AddAsync(user, ct);

    public void Update(User user) => _ctx.Users.Update(user);
}
