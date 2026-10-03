using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.Services;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Projects.Attachments;

public record UploadAttachmentCommand(Guid TaskId, string FileName, string ContentType, long Size, Stream Content)
    : IRequest<Result<AttachmentDto>>;

/// <summary>Deletes an attachment. Allowed for the uploader and for workspace owners/admins.</summary>
public record DeleteAttachmentCommand(Guid AttachmentId) : IRequest<Result>;

public record AttachmentDownload(string FileName, string ContentType, bool IsPreviewableImage, MemoryStream Content);

public record GetAttachmentContentQuery(Guid AttachmentId) : IRequest<Result<AttachmentDownload>>;

public class AttachmentHandlers :
    IRequestHandler<UploadAttachmentCommand, Result<AttachmentDto>>,
    IRequestHandler<DeleteAttachmentCommand, Result>,
    IRequestHandler<GetAttachmentContentQuery, Result<AttachmentDownload>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;
    private readonly ILogger<AttachmentHandlers> _logger;

    public AttachmentHandlers(IUnitOfWork uow, ICurrentUser currentUser, IFileStorage storage, ILogger<AttachmentHandlers> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Result<AttachmentDto>> Handle(UploadAttachmentCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<AttachmentDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        if (request.Size <= 0)
            return Result.Failure<AttachmentDto>(Error.Validation("Attachment.Empty", "The file is empty"));
        if (request.Size > AttachmentRules.MaxFileSizeBytes)
            return Result.Failure<AttachmentDto>(Error.Validation("Attachment.TooLarge",
                $"Files can be at most {AttachmentRules.MaxFileSizeBytes / (1024 * 1024)} MB"));

        var task = await _uow.Tasks.GetByIdAsync(request.TaskId, ct);
        if (task == null || task.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<AttachmentDto>(Error.NotFound("Task.NotFound", "Task not found"));

        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType;
        var displayName = Path.GetFileName(request.FileName);
        var key = $"{task.TenantId}/{task.Id}/{Guid.NewGuid():N}-{AttachmentRules.SafeFileName(displayName)}";

        var attachmentResult = TaskAttachment.Create(task.TenantId, task.Id, _currentUser.UserId.Value,
            displayName, key, contentType, request.Size);
        if (attachmentResult.IsFailure) return Result.Failure<AttachmentDto>(attachmentResult.Error);

        // Store the bytes first; only record the attachment once the file really exists.
        var upload = await _storage.UploadAsync(key, request.Content, request.Size, contentType, ct);
        if (upload.IsFailure) return Result.Failure<AttachmentDto>(upload.Error);

        var attachment = attachmentResult.Value;
        try
        {
            await _uow.Tasks.AddAttachmentAsync(attachment, ct);
            task.IncrementAttachmentCount();
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            // Don't leave an orphaned object in storage if the database write fails.
            await _storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        var uploader = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
        return Result.Success(new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType,
            attachment.FileSizeBytes, attachment.UploadedById, uploader?.FullName ?? "You", attachment.CreatedAt,
            AttachmentRules.IsPreviewableImage(attachment.ContentType)));
    }

    public async Task<Result> Handle(DeleteAttachmentCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var attachment = await _uow.Tasks.GetAttachmentByIdAsync(request.AttachmentId, ct);
        if (attachment == null || attachment.TenantId != _currentUser.TenantId.Value)
            return Result.Failure(Error.NotFound("Attachment.NotFound", "Attachment not found"));

        if (attachment.UploadedById != _currentUser.UserId.Value)
        {
            var admin = await WorkspaceAccess.RequireAdminAsync(_uow, _currentUser, ct);
            if (admin.IsFailure)
                return Result.Failure(Error.Forbidden("Attachment.NotOwner", "Only the uploader or a workspace admin can delete this file"));
        }

        var task = await _uow.Tasks.GetByIdAsync(attachment.TaskId, ct);
        task?.DecrementAttachmentCount();
        _uow.Tasks.RemoveAttachment(attachment);
        await _uow.SaveChangesAsync(ct);

        // The record is gone either way; a failed object delete only leaves an unreachable file behind.
        var deleted = await _storage.DeleteAsync(attachment.StorageKey, ct);
        if (deleted.IsFailure)
            _logger.LogWarning("Attachment {AttachmentId} removed but its stored object {Key} could not be deleted",
                attachment.Id, attachment.StorageKey);

        return Result.Success();
    }

    public async Task<Result<AttachmentDownload>> Handle(GetAttachmentContentQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<AttachmentDownload>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var attachment = await _uow.Tasks.GetAttachmentByIdAsync(request.AttachmentId, ct);
        if (attachment == null || attachment.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<AttachmentDownload>(Error.NotFound("Attachment.NotFound", "Attachment not found"));

        var buffer = new MemoryStream();
        var download = await _storage.DownloadAsync(attachment.StorageKey, buffer, ct);
        if (download.IsFailure)
        {
            await buffer.DisposeAsync();
            return Result.Failure<AttachmentDownload>(download.Error);
        }

        buffer.Position = 0;
        return Result.Success(new AttachmentDownload(attachment.FileName, attachment.ContentType,
            AttachmentRules.IsPreviewableImage(attachment.ContentType), buffer));
    }
}
