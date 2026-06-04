using FlowForge.Domain.Identity.Events;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Identity;

public sealed class Tenant : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public string PlanTier { get; private set; } = "Free";
    public int MaxMembers { get; private set; } = 5;
    public int MaxProjects { get; private set; } = 3;

    private readonly List<Membership> _memberships = new();
    public IReadOnlyCollection<Membership> Memberships => _memberships.AsReadOnly();

    private Tenant() { }

    public static Result<Tenant> Create(string name, string slug, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure<Tenant>(Error.Validation("Tenant.InvalidName", "Name must be 1-100 characters"));

        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 50)
            return Result.Failure<Tenant>(Error.Validation("Tenant.InvalidSlug", "Slug must be 1-50 characters"));

        var tenant = new Tenant
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            IsActive = true
        };

        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.Name, ownerId));
        return Result.Success(tenant);
    }

    public Result UpdateProfile(string name, string? description, string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure(Error.Validation("Tenant.InvalidName", "Name must be 1-100 characters"));

        Name = name.Trim();
        Description = description;
        LogoUrl = logoUrl;
        Touch();
        return Result.Success();
    }

    public Result Suspend(string reason)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict("Tenant.AlreadySuspended", "Tenant already suspended"));

        IsActive = false;
        SuspendedAt = DateTime.UtcNow;
        SuspensionReason = reason;
        Touch();
        return Result.Success();
    }

    public Result Reactivate()
    {
        IsActive = true;
        SuspendedAt = null;
        SuspensionReason = null;
        Touch();
        return Result.Success();
    }

    public Result UpgradePlan(string planTier, int maxMembers, int maxProjects)
    {
        PlanTier = planTier;
        MaxMembers = maxMembers;
        MaxProjects = maxProjects;
        Touch();
        return Result.Success();
    }

    public bool CanAddMember() => _memberships.Count < MaxMembers;
}
