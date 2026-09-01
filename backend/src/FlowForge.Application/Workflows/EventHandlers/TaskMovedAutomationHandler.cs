using FlowForge.Application.Notifications.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Enums;
using FlowForge.Domain.Projects.Events;
using FlowForge.Domain.Workflows.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Workflows.EventHandlers;

/// <summary>
/// Evaluates a project's automation rules whenever a task is moved between lists.
/// This is what turns "when task moved to Done, notify the manager" from the docs
/// into a real, working effect: MoveTaskCommand raises TaskMovedEvent -> MediatR
/// publishes it after SaveChanges -> this handler runs the matching rules.
/// </summary>
public class TaskMovedAutomationHandler : INotificationHandler<TaskMovedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<TaskMovedAutomationHandler> _logger;

    public TaskMovedAutomationHandler(IUnitOfWork uow, INotificationDispatcher dispatcher, ILogger<TaskMovedAutomationHandler> logger)
    {
        _uow = uow;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Handle(TaskMovedEvent notification, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdAsync(notification.TaskId, ct);
        if (task == null) return;

        var rules = await _uow.AutomationRules.GetEnabledByTriggerAsync(task.ProjectId, notification.ToListId, ct);
        var matching = rules.Where(r => r.TriggerType == AutomationTriggerType.TaskMovedToList).ToList();
        if (matching.Count == 0) return;

        var project = await _uow.Projects.GetByIdAsync(task.ProjectId, ct);
        var projectName = project?.Name ?? "a project";
        var tasksChanged = false;
        var toDispatch = new List<Notification>();

        foreach (var rule in matching)
        {
            NotificationType notifType;
            string title;
            string message;

            if (rule.ActionType == AutomationActionType.AssignUser)
            {
                var assignResult = task.Assign(rule.ActionUserId, notification.MovedById, notification.TenantId);
                if (assignResult.IsFailure)
                {
                    _logger.LogWarning("Automation rule {RuleId} failed to assign task {TaskId}: {Error}",
                        rule.Id, task.Id, assignResult.Error.Message);
                    continue;
                }
                tasksChanged = true;
                notifType = NotificationType.TaskAssigned;
                title = "You were assigned a task";
                message = $"\"{task.Title}\" ({projectName}) was assigned to you by automation \"{rule.Name}\".";
            }
            else
            {
                notifType = NotificationType.AutomationTriggered;
                title = "Automation triggered";
                message = $"\"{task.Title}\" ({projectName}) triggered automation \"{rule.Name}\".";
            }

            var notifResult = Notification.Create(
                notification.TenantId, rule.ActionUserId, notifType, title, message,
                relatedTaskId: task.Id, relatedProjectId: task.ProjectId);

            if (notifResult.IsFailure) continue;

            var created = notifResult.Value;
            await _uow.Notifications.AddAsync(created, ct);
            toDispatch.Add(created);
        }

        if (tasksChanged) _uow.Tasks.Update(task);
        await _uow.SaveChangesAsync(ct);

        // Dispatch (real-time + email + Slack) only after the notifications are durably
        // saved, so a client never sees an alert for something that turned out not to persist.
        foreach (var created in toDispatch)
            await _dispatcher.DispatchAsync(created, ct);
    }
}
