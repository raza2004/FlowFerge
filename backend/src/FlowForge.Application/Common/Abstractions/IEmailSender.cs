using FlowForge.Shared.Results;

namespace FlowForge.Application.Common.Abstractions;

public interface IEmailSender
{
    Task<Result> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
