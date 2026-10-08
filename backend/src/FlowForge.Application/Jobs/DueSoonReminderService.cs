using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Notifications.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Enums;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Jobs;

/// <summary>
/// Reminds assignees about unfinished tasks due today or tomorrow. Meant to run once a day, so a
/// task gets a heads-up the day before and another on the day. A reminder already sent in the
/// last 20 hours is skipped, which makes a retry or a manual re-run harmless.
/// </summary>
public class DueSoonReminderService
{
    private static readonly TimeSpan MinimumGap = TimeSpan.FromHours(20);

    private readonly IUnitOfWork _uow;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DueSoonReminderService> _logger;

    public DueSoonReminderService(
        IUnitOfWork uow, INotificationDispatcher dispatcher, IDateTimeProvider clock, ILogger<DueSoonReminderService> logger)
    {
        _uow = uow;
        _dispatcher = dispatcher;
        _clock = clock;
        _logger = logger;
    }

    /// <returns>How many reminders were sent.</returns>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var today = now.Date;

        var tasks = (await _uow.Tasks.GetOpenAssignedDueBetweenAsync(today, today.AddDays(2), ct)).ToList();
        var projectNames = new Dictionary<Guid, string>();
        var created = new List<Notification>();

        foreach (var task in tasks)
        {
            var userId = task.AssigneeId!.Value;
            if (await _uow.Notifications.ExistsAsync(userId, task.Id, NotificationType.DueSoon, now - MinimumGap, ct))
                continue;

            if (!projectNames.TryGetValue(task.ProjectId, out var projectName))
            {
                projectName = (await _uow.Projects.GetByIdAsync(task.ProjectId, ct))?.Name ?? "a project";
                projectNames[task.ProjectId] = projectName;
            }

            var dueToday = task.DueDate!.Value.Date == today;
            var result = Notification.Create(
                task.TenantId, userId, NotificationType.DueSoon,
                dueToday ? $"{task.TaskNumber} is due today" : $"{task.TaskNumber} is due tomorrow",
                $"\"{task.Title}\" ({projectName}) is {(dueToday ? "due today" : "due tomorrow")}.",
                relatedTaskId: task.Id, relatedProjectId: task.ProjectId);
            if (result.IsFailure) continue;

            await _uow.Notifications.AddAsync(result.Value, ct);
            created.Add(result.Value);
        }

        if (created.Count == 0) return 0;

        await _uow.SaveChangesAsync(ct);

        // Each notification is delivered independently, so one bad delivery can't block the rest.
        foreach (var notification in created)
        {
            try
            {
                await _dispatcher.DispatchAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deliver due-soon reminder {NotificationId}", notification.Id);
            }
        }

        _logger.LogInformation("Sent {Count} due-soon reminders", created.Count);
        return created.Count;
    }
}
