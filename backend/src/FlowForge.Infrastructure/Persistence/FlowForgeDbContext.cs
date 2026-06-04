using System.Reflection;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Auditing;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Primitives;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence;

public class FlowForgeDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IPublisher? _publisher;

    public FlowForgeDbContext(
        DbContextOptions<FlowForgeDbContext> options,
        ITenantContext? tenantContext = null,
        IPublisher? publisher = null) : base(options)
    {
        _tenantContext = tenantContext;
        _publisher = publisher;
    }

    // Identity
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardList> BoardLists => Set<BoardList>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<TaskLabel> TaskLabels => Set<TaskLabel>();
    public DbSet<TaskWatcher> TaskWatchers => Set<TaskWatcher>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Sprint> Sprints => Set<Sprint>();

    // Auditing
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Touch UpdatedAt on modified entities
        var entries = ChangeTracker.Entries<Entity>().Where(e => e.State == EntityState.Modified);
        foreach (var entry in entries)
            entry.Entity.Touch();

        // Collect domain events before saving
        var domainEvents = ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(ct);

        // Publish domain events after saving
        if (_publisher != null)
        {
            foreach (var domainEvent in domainEvents)
                await _publisher.Publish(domainEvent, ct);
        }

        // Clear events
        foreach (var entry in ChangeTracker.Entries<Entity>())
            entry.Entity.ClearDomainEvents();

        return result;
    }
}
