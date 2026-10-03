using FlowForge.Application.Notifications.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Enums;
using FlowForge.Domain.Projects.Events;
using MediatR;

namespace FlowForge.Application.Notifications.EventHandlers;

/// <summary>
/// Turns a new comment into notifications: anyone @mentioned gets a "Mentioned" notification,
/// and the task's assignee and watchers get a "CommentAdded" one. Each person is notified at
/// most once, and the author is never notified about their own comment.
/// </summary>
public class CommentAddedNotificationHandler : INotificationHandler<CommentAddedEvent>
{
    private const int PreviewLength = 140;

    private readonly IUnitOfWork _uow;
    private readonly INotificationDispatcher _dispatcher;

    public CommentAddedNotificationHandler(IUnitOfWork uow, INotificationDispatcher dispatcher)
    {
        _uow = uow;
        _dispatcher = dispatcher;
    }

    public async Task Handle(CommentAddedEvent evt, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdWithDetailsAsync(evt.TaskId, ct);
        if (task == null) return;

        var author = await _uow.Users.GetByIdAsync(evt.AuthorId, ct);
        var authorName = author?.FullName ?? "Someone";
        var preview = evt.Content.Length > PreviewLength ? evt.Content[..PreviewLength] + "…" : evt.Content;

        var recipients = new Dictionary<Guid, NotificationType>();
        foreach (var id in evt.MentionedUserIds)
            recipients[id] = NotificationType.Mentioned;

        var followers = task.Watchers.Select(w => w.UserId).ToList();
        if (task.AssigneeId.HasValue) followers.Add(task.AssigneeId.Value);
        foreach (var id in followers)
            recipients.TryAdd(id, NotificationType.CommentAdded);

        recipients.Remove(evt.AuthorId);
        if (recipients.Count == 0) return;

        var created = new List<Notification>();
        foreach (var (userId, type) in recipients)
        {
            var title = type == NotificationType.Mentioned
                ? $"{authorName} mentioned you on {task.TaskNumber}"
                : $"{authorName} commented on {task.TaskNumber}";

            var result = Notification.Create(evt.TenantId, userId, type, title, preview,
                relatedTaskId: task.Id, relatedProjectId: task.ProjectId);
            if (result.IsFailure) continue;

            await _uow.Notifications.AddAsync(result.Value, ct);
            created.Add(result.Value);
        }

        await _uow.SaveChangesAsync(ct);

        foreach (var notification in created)
            await _dispatcher.DispatchAsync(notification, ct);
    }
}
