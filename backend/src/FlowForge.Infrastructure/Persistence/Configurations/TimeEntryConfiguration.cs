using FlowForge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> b)
    {
        b.ToTable("time_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.TaskId);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.WorkDate });

        b.HasOne<ProjectTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
