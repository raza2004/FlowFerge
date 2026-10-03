using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Notifications.DTOs;
using FlowForge.Application.Notifications.Messages;
using FlowForge.Domain.Common;
using FlowForge.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Notifications.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IUnitOfWork _uow;
    private readonly IRealtimeNotifier _realtime;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IUnitOfWork uow, IRealtimeNotifier realtime, IMessageBus messageBus,
        ILogger<NotificationDispatcher> logger)
    {
        _uow = uow;
        _realtime = realtime;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task DispatchAsync(Notification notification, CancellationToken ct = default)
    {
        var dto = new NotificationDto(
            notification.Id, notification.Type, notification.Title, notification.Message,
            notification.RelatedTaskId, notification.RelatedProjectId, notification.IsRead, notification.CreatedAt);

        // Real-time is synchronous and unconditional - it's cheap, local, and the user is
        // plausibly looking at the screen right now. Email/Slack are slow, external I/O that
        // can time out or rate-limit, so those go on a RabbitMQ queue instead: this request
        // finishes immediately, and FlowForge.Workers delivers them independently, with a
        // message broker's at-least-once retry semantics instead of "hope the SMTP call works."
        await _realtime.NotifyUserAsync(notification.UserId, "NotificationReceived", dto, ct);

        var user = await _uow.Users.GetByIdAsync(notification.UserId, ct);
        var tenant = await _uow.Tenants.GetByIdAsync(notification.TenantId, ct);

        var recipientEmail = user is { EmailNotificationsEnabled: true } ? user.Email.Value : null;
        var slackWebhookUrl = string.IsNullOrWhiteSpace(tenant?.SlackWebhookUrl) ? null : tenant.SlackWebhookUrl;

        if (recipientEmail == null && slackWebhookUrl == null)
            return;

        try
        {
            await _messageBus.PublishAsync(
                new DeliverNotificationMessage(notification.Id, recipientEmail, notification.Title, notification.Message, slackWebhookUrl),
                ct);
        }
        catch (Exception ex)
        {
            // RabbitMQ being unreachable should never fail the request that triggered this -
            // realtime delivery above already happened, email/Slack are best-effort add-ons.
            _logger.LogWarning(ex, "Failed to publish DeliverNotificationMessage for notification {NotificationId}", notification.Id);
        }
    }
}
