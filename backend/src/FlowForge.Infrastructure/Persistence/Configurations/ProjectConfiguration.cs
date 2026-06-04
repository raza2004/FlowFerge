using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(5000);
        b.Property(x => x.Color).HasMaxLength(20);
        b.Property(x => x.IconUrl).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Visibility).HasConversion<int>();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.TenantId);
        b.HasQueryFilter(x => !x.IsDeleted);

        b.Property(x => x.Key)
         .HasConversion(k => k.Value, v => ProjectKey.Create(v).Value)
         .HasMaxLength(10)
         .IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();

        b.HasMany(x => x.Boards)
         .WithOne()
         .HasForeignKey(x => x.ProjectId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Members)
         .WithOne()
         .HasForeignKey(x => x.ProjectId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Labels)
         .WithOne()
         .HasForeignKey(x => x.ProjectId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
