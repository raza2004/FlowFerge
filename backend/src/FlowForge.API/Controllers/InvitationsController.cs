using FlowForge.API.Common;
using FlowForge.Application.Identity.Commands;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public InvitationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetPending()
    {
        var result = await _mediator.Send(new GetPendingInvitationsQuery());
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationRequest body)
    {
        var result = await _mediator.Send(new CreateInvitationCommand(body.Email, body.Role));
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var result = await _mediator.Send(new RevokeInvitationCommand(id));
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("by-token/{token}")]
    public async Task<IActionResult> Preview(string token)
    {
        var result = await _mediator.Send(new GetInvitationPreviewQuery(token));
        return result.ToActionResult();
    }

    [HttpPost("by-token/{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        var result = await _mediator.Send(new AcceptInvitationCommand(token));
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("by-token/{token}/register")]
    public async Task<IActionResult> Register(string token, [FromBody] RegisterWithInvitationRequest body)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers["User-Agent"].ToString();
        var result = await _mediator.Send(new RegisterWithInvitationCommand(
            token, body.FirstName, body.LastName, body.Password, ip, ua));
        return result.ToActionResult();
    }
}
