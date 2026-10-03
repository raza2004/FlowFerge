namespace FlowForge.Application.Notifications.Messages;

/// <summary>
/// Published to RabbitMQ (via MassTransit) whenever a notification needs to go out over a
/// slow, best-effort channel (email, Slack). SignalR stays synchronous in NotificationDispatcher
/// because it's cheap and immediate; email/Slack are queued so an SMTP timeout or a flaky
/// webhook can never slow down the request that triggered the notification.
/// </summary>
public record DeliverNotificationMessage(
    Guid NotificationId,
    string? RecipientEmail,
    string Title,
    string Message,
    string? SlackWebhookUrl
);
