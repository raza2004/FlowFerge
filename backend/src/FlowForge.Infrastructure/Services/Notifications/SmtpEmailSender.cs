using System.Net.Mail;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowForge.Infrastructure.Services.Notifications;

/// <summary>
/// Sends via plain SMTP - points at MailHog in development (no auth/TLS needed), and
/// at any real SMTP relay in production via the same Email:* config keys. Failures are
/// logged and returned as a Result rather than thrown, since a downed mail relay should
/// never take out the request that triggered the notification.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<Result> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
            return Result.Failure(Error.Failure("Email.NotConfigured", "Email:SmtpHost is not configured"));

        var port = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 1025;
        var fromAddress = _config["Email:FromAddress"] ?? "noreply@flowforge.local";
        var fromName = _config["Email:FromName"] ?? "FlowForge";

        try
        {
            using var client = new SmtpClient(host, port);
            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message, ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {ToEmail} via {Host}:{Port}", toEmail, host, port);
            return Result.Failure(Error.Failure("Email.SendFailed", "Failed to send email"));
        }
    }
}
