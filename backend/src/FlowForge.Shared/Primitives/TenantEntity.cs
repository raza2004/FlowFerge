namespace FlowForge.Shared.Primitives;

public abstract class TenantEntity : AggregateRoot
{
    public Guid TenantId { get; protected set; }
}
