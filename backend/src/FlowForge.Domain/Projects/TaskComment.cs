using FlowForge.Domain.Projects.Events;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class TaskComment : SoftDeletableEntity
{
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsEdited { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public Guid? ParentCommentId { get; private set; }

    private readonly List<Guid> _mentionedUserIds = new();
    public IReadOnlyCollection<Guid> MentionedUserIds => _mentionedUserIds.AsReadOnly();

    private TaskComment() { }

    public static Result<TaskComment> Create(
        Guid tenantId,
        Guid taskId,
        Guid authorId,
        string content,
        List<Guid>? mentionedUserIds = null,
        Guid? parentCommentId = null)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 10000)
            return Result.Failure<TaskComment>(Error.Validation("Comment.InvalidContent", "Content must be 1-10000 chars"));

        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content,
            ParentCommentId = parentCommentId
        };

        comment.GetType().GetProperty("TenantId")!.SetValue(comment, tenantId);

        if (mentionedUserIds != null)
            comment._mentionedUserIds.AddRange(mentionedUserIds);

        comment.RaiseDomainEvent(new CommentAddedEvent(comment.Id, taskId, authorId, content, mentionedUserIds ?? new(), tenantId));
        return Result.Success(comment);
    }

    public Result Edit(string newContent, Guid editorId)
    {
        if (editorId != AuthorId)
            return Result.Failure(Error.Forbidden("Comment.NotAuthor", "Only the author can edit"));

        if (string.IsNullOrWhiteSpace(newContent) || newContent.Length > 10000)
            return Result.Failure(Error.Validation("Comment.InvalidContent", "Content must be 1-10000 chars"));

        Content = newContent;
        IsEdited = true;
        EditedAt = DateTime.UtcNow;
        Touch();
        return Result.Success();
    }
}
