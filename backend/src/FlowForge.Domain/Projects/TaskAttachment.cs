using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class TaskAttachment : Entity
{
    public Guid TaskId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UploadedById { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string? ThumbnailUrl { get; private set; }

    private TaskAttachment() { }

    public static Result<TaskAttachment> Create(
        Guid tenantId,
        Guid taskId,
        Guid uploadedById,
        string fileName,
        string storageKey,
        string contentType,
        long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 500)
            return Result.Failure<TaskAttachment>(Error.Validation("Attachment.InvalidFileName", "Filename required, max 500 chars"));

        if (fileSizeBytes <= 0 || fileSizeBytes > 100 * 1024 * 1024)
            return Result.Failure<TaskAttachment>(Error.Validation("Attachment.InvalidSize", "File must be 1 byte - 100 MB"));

        return Result.Success(new TaskAttachment
        {
            TenantId = tenantId,
            TaskId = taskId,
            UploadedById = uploadedById,
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes
        });
    }

    public void SetThumbnail(string thumbnailUrl)
    {
        ThumbnailUrl = thumbnailUrl;
        Touch();
    }
}
