using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email)
         .HasConversion(e => e.Value, v => Email.Create(v).Value)
         .HasMaxLength(256)
         .IsRequired();
        b.Property(x => x.Role).HasConversion<int>();
        b.Property(x => x.Token).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Email });
    }
}
