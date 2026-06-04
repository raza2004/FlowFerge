using FlowForge.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.PlanTier).HasMaxLength(50);
        b.Property(x => x.SuspensionReason).HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();

        b.HasMany(x => x.Memberships)
         .WithOne(x => x.Tenant)
         .HasForeignKey(x => x.TenantId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
