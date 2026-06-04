namespace FlowForge.Shared.Primitives;

public abstract class SoftDeletableEntity : TenantEntity
{
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }
    public Guid? DeletedById { get; protected set; }

    public void SoftDelete(Guid userId)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedById = userId;
        Touch();
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedById = null;
        Touch();
    }
}
