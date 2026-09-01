using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Notifications.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FlowForge.Application.Notifications.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IUnitOfWork _uow;
    private readonly IRealtimeNotifier _realtime;
    private readonly IEmailSender _email;
    private readonly ISlackNotifier _slack;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IUnitOfWork uow, IRealtimeNotifier realtime, IEmailSender email, ISlackNotifier slack,
        ILogger<NotificationDispatcher> logger)
    {
        _uow = uow;
        _realtime = realtime;
        _email = email;
        _slack = slack;
        _logger = logger;
    }

    public async Task DispatchAsync(Notification notification, CancellationToken ct = default)
    {
        var dto = new NotificationDto(
            notification.Id, notification.Type, notification.Title, notification.Message,
            notification.RelatedTaskId, notification.RelatedProjectId, notification.IsRead, notification.CreatedAt);

        // Real-time is unconditional - every other channel is a "also send it here" add-on.
        await _realtime.NotifyUserAsync(notification.UserId, "NotificationReceived", dto, ct);

        var user = await _uow.Users.GetByIdAsync(notification.UserId, ct);
        if (user != null && user.EmailNotificationsEnabled)
        {
            var emailBody =
                $"<p><strong>{System.Net.WebUtility.HtmlEncode(notification.Title)}</strong></p>" +
                $"<p>{System.Net.WebUtility.HtmlEncode(notification.Message)}</p>" +
                "<p style=\"color:#888;font-size:12px\">Sent by FlowForge</p>";

            var emailResult = await _email.SendAsync(user.Email.Value, notification.Title, emailBody, ct);
            if (emailResult.IsFailure)
                _logger.LogWarning("Email notification to {Email} failed: {Error}", user.Email.Value, emailResult.Error.Message);
        }

        var tenant = await _uow.Tenants.GetByIdAsync(notification.TenantId, ct);
        if (!string.IsNullOrWhiteSpace(tenant?.SlackWebhookUrl))
        {
            var slackText = $"*{notification.Title}*\n{notification.Message}";
            var slackResult = await _slack.PostAsync(tenant.SlackWebhookUrl, slackText, ct);
            if (slackResult.IsFailure)
                _logger.LogWarning("Slack notification for tenant {TenantId} failed: {Error}", tenant.Id, slackResult.Error.Message);
        }
    }
}
