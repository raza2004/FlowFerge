using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

/// <summary>
/// Adds a comment and makes the author a watcher. Mentioned user ids are filtered down to
/// real members of the current tenant, so a client can't notify arbitrary users. The
/// notifications themselves are sent by CommentAddedNotificationHandler, off the
/// CommentAddedEvent this raises, the same way automations react to TaskMovedEvent.
/// </summary>
public record AddCommentCommand(Guid TaskId, string Content, List<Guid>? MentionedUserIds) : IRequest<Result<TaskCommentDto>>;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(10000);
    }
}

public class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, Result<TaskCommentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public AddCommentCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TaskCommentDto>> Handle(AddCommentCommand request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<TaskCommentDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var tenantId = _currentUser.TenantId.Value;
        var authorId = _currentUser.UserId.Value;

        var task = await _uow.Tasks.GetByIdWithDetailsAsync(request.TaskId, ct);
        if (task == null || task.TenantId != tenantId)
            return Result.Failure<TaskCommentDto>(Error.NotFound("Task.NotFound", "Task not found"));

        var members = (await _uow.Users.GetByTenantAsync(tenantId, ct)).ToDictionary(u => u.Id, u => u.FullName);
        var mentions = (request.MentionedUserIds ?? new())
            .Distinct()
            .Where(id => id != authorId && members.ContainsKey(id))
            .ToList();

        var commentResult = TaskComment.Create(tenantId, task.Id, authorId, request.Content.Trim(), mentions);
        if (commentResult.IsFailure) return Result.Failure<TaskCommentDto>(commentResult.Error);

        var comment = commentResult.Value;
        await _uow.TaskComments.AddAsync(comment, ct);
        task.IncrementCommentCount();
        task.AddWatcher(authorId);

        await _uow.SaveChangesAsync(ct);

        return Result.Success(new TaskCommentDto(
            comment.Id, authorId, members.GetValueOrDefault(authorId, "Unknown user"),
            comment.Content, comment.IsEdited, comment.CreatedAt, mentions));
    }
}
