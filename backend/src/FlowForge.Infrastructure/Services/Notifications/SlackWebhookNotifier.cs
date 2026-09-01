using System.Net.Http.Json;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;
using Microsoft.Extensions.Logging;

namespace FlowForge.Infrastructure.Services.Notifications;

/// <summary>
/// Posts to a Slack "Incoming Webhook" URL (one per tenant, configured in Settings).
/// This is the standard no-OAuth way to get messages into a Slack channel - the tenant
/// creates a webhook in their own Slack workspace and pastes the URL in; there's no
/// FlowForge-side Slack app or bot token involved.
/// </summary>
public class SlackWebhookNotifier : ISlackNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackWebhookNotifier> _logger;

    public SlackWebhookNotifier(IHttpClientFactory httpClientFactory, ILogger<SlackWebhookNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Result> PostAsync(string webhookUrl, string text, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(SlackWebhookNotifier));
            var response = await client.PostAsJsonAsync(webhookUrl, new { text }, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Slack webhook returned {Status}: {Body}", (int)response.StatusCode, body);
                return Result.Failure(Error.Failure("Slack.SendFailed", $"Slack webhook returned {(int)response.StatusCode}"));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post to Slack webhook");
            return Result.Failure(Error.Failure("Slack.SendFailed", "Failed to reach the Slack webhook"));
        }
    }
}
