using FlowForge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class BoardListConfiguration : IEntityTypeConfiguration<BoardList>
{
    public void Configure(EntityTypeBuilder<BoardList> b)
    {
        b.ToTable("board_lists");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Color).HasMaxLength(20);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.BoardId);

        b.HasMany(x => x.Tasks)
         .WithOne()
         .HasForeignKey(x => x.ListId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
