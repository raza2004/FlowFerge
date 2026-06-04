using FlowForge.Application.Common.Abstractions;

namespace FlowForge.Infrastructure.Services.Auth;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId) => TenantId = tenantId;
    public void ClearTenant() => TenantId = null;
}
