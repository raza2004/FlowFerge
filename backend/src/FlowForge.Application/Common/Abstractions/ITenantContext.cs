namespace FlowForge.Application.Common.Abstractions;

public interface ITenantContext
{
    Guid? TenantId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
    void ClearTenant();
}
