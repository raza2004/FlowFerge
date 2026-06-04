using FlowForge.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Token).IsUnique();
        b.Property(x => x.RevokedReason).HasMaxLength(200);
        b.Property(x => x.CreatedByIp).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(500);
    }
}
