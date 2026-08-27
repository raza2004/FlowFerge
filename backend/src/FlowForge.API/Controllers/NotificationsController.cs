using FlowForge.API.Common;
using FlowForge.Application.Notifications.Commands;
using FlowForge.Application.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var result = await _mediator.Send(new GetMyNotificationsQuery());
        return result.ToActionResult();
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var result = await _mediator.Send(new GetUnreadNotificationCountQuery());
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var result = await _mediator.Send(new MarkNotificationReadCommand(id));
        return result.ToActionResult();
    }
}
