using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly FlowForgeDbContext _ctx;
    public TenantRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        _ctx.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        _ctx.Tenants.AnyAsync(t => t.Slug == slug, ct);

    public async Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken ct = default) =>
        await _ctx.Tenants.ToListAsync(ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default) =>
        await _ctx.Tenants.AddAsync(tenant, ct);

    public void Update(Tenant tenant) => _ctx.Tenants.Update(tenant);
    public void Delete(Tenant tenant) => _ctx.Tenants.Remove(tenant);
}
