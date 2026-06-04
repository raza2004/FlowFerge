using FlowForge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> b)
    {
        b.ToTable("task_comments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).IsRequired().HasMaxLength(10000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => x.TaskId);

        b.Property<List<Guid>>("_mentionedUserIds")
         .HasField("_mentionedUserIds")
         .HasColumnName("mentioned_user_ids");
    }
}
