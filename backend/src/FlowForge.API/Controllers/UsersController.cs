using FlowForge.API.Common;
using FlowForge.Application.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var result = await _mediator.Send(new GetCurrentUserQuery());
        return result.ToActionResult();
    }

    [HttpGet("members")]
    public async Task<IActionResult> Members()
    {
        var result = await _mediator.Send(new GetTenantMembersQuery());
        return result.ToActionResult();
    }
}
