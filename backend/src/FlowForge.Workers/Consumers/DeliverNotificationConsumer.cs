using System.Net;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Notifications.Messages;
using MassTransit;

namespace FlowForge.Workers.Consumers;

/// <summary>
/// Consumes DeliverNotificationMessage from RabbitMQ and actually sends the email/Slack
/// message. This is the other half of NotificationDispatcher (FlowForge.Application) - the
/// API publishes and returns immediately, this runs independently in the Workers process.
/// </summary>
public class DeliverNotificationConsumer : IConsumer<DeliverNotificationMessage>
{
    private readonly IEmailSender _email;
    private readonly ISlackNotifier _slack;
    private readonly ILogger<DeliverNotificationConsumer> _logger;

    public DeliverNotificationConsumer(IEmailSender email, ISlackNotifier slack, ILogger<DeliverNotificationConsumer> logger)
    {
        _email = email;
        _slack = slack;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeliverNotificationMessage> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (!string.IsNullOrWhiteSpace(msg.RecipientEmail))
        {
            var emailBody =
                $"<p><strong>{WebUtility.HtmlEncode(msg.Title)}</strong></p>" +
                $"<p>{WebUtility.HtmlEncode(msg.Message)}</p>" +
                "<p style=\"color:#888;font-size:12px\">Sent by FlowForge</p>";

            var emailResult = await _email.SendAsync(msg.RecipientEmail, msg.Title, emailBody, ct);
            if (emailResult.IsFailure)
                _logger.LogWarning("Email notification to {Email} failed: {Error}", msg.RecipientEmail, emailResult.Error.Message);
        }

        if (!string.IsNullOrWhiteSpace(msg.SlackWebhookUrl))
        {
            var slackText = $"*{msg.Title}*\n{msg.Message}";
            var slackResult = await _slack.PostAsync(msg.SlackWebhookUrl, slackText, ct);
            if (slackResult.IsFailure)
                _logger.LogWarning("Slack notification for {NotificationId} failed: {Error}", msg.NotificationId, slackResult.Error.Message);
        }
    }
}
