using FlowForge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> b)
    {
        b.ToTable("sprints");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Goal).HasMaxLength(2000);
        b.Property(x => x.RetrospectiveNotes).HasMaxLength(10000);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.ProjectId);
    }
}
