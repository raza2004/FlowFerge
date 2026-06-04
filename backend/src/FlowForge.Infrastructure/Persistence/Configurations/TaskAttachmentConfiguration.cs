using FlowForge.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowForge.Infrastructure.Persistence.Configurations;

public class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> b)
    {
        b.ToTable("task_attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        b.Property(x => x.StorageKey).IsRequired().HasMaxLength(500);
        b.Property(x => x.ContentType).IsRequired().HasMaxLength(200);
        b.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        b.HasIndex(x => x.TaskId);
    }
}
