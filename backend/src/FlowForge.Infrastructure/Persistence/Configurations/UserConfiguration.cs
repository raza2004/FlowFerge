using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(x => x.AvatarUrl).HasMaxLength(500);
        b.Property(x => x.Bio).HasMaxLength(1000);
        b.Property(x => x.PhoneNumber).HasMaxLength(50);
        b.Property(x => x.EmailVerificationToken).HasMaxLength(100);
        b.Property(x => x.Version).IsConcurrencyToken();

        // Email is a value object — store as string
        b.Property(x => x.Email)
         .HasConversion(e => e.Value, v => Email.Create(v).Value)
         .HasMaxLength(256)
         .IsRequired();
        b.HasIndex(x => x.Email).IsUnique();

        b.HasMany(x => x.Memberships)
         .WithOne(x => x.User)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
