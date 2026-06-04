using FlowForge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> b)
    {
        b.ToTable("tasks");
        b.HasKey(x => x.Id);
        b.Property(x => x.TaskNumber).IsRequired().HasMaxLength(20);
        b.Property(x => x.Title).IsRequired().HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(20000);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Priority).HasConversion<int>();
        b.Property(x => x.Status).IsRequired().HasMaxLength(50);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.ListId);
        b.HasIndex(x => x.AssigneeId);
        b.HasIndex(x => new { x.ProjectId, x.TaskNumber }).IsUnique();

        b.HasMany(x => x.Comments).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Labels).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Watchers).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Subtasks).WithOne().HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
