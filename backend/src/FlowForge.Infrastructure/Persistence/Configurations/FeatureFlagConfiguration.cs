using FlowForge.Domain.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> b)
    {
        b.ToTable("feature_flags");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasMany(x => x.Overrides).WithOne().HasForeignKey(o => o.FeatureFlagId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FeatureFlagOverrideConfiguration : IEntityTypeConfiguration<FeatureFlagOverride>
{
    public void Configure(EntityTypeBuilder<FeatureFlagOverride> b)
    {
        b.ToTable("feature_flag_overrides");
        b.HasKey(x => new { x.FeatureFlagId, x.TenantId });
    }
}
