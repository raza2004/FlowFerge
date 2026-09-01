using FlowForge.API.Common;
using FlowForge.Application.Identity.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/settings")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SettingsController(IMediator mediator) => _mediator = mediator;

    [HttpPut("slack-webhook")]
    public async Task<IActionResult> UpdateSlackWebhook([FromBody] UpdateSlackWebhookRequest req)
    {
        var result = await _mediator.Send(new UpdateSlackWebhookCommand(req.WebhookUrl));
        return result.ToActionResult();
    }

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesCommand cmd)
    {
        var result = await _mediator.Send(cmd);
        return result.ToActionResult();
    }
}

public record UpdateSlackWebhookRequest(string? WebhookUrl);
