using FlowForge.API.Common;
using FlowForge.Application.Identity.Commands;
using FlowForge.Application.Identity.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers["User-Agent"].ToString();
        var result = await _mediator.Send(new RegisterCommand(
            req.FirstName, req.LastName, req.Email, req.Password,
            req.TenantName, req.TenantSlug, ip, ua));
        return result.ToActionResult();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers["User-Agent"].ToString();
        var result = await _mediator.Send(new LoginCommand(req.Email, req.Password, ip, ua));
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers["User-Agent"].ToString();
        var result = await _mediator.Send(new RefreshTokenCommand(req.RefreshToken, ip, ua));
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest req)
    {
        var result = await _mediator.Send(new LogoutCommand(req.RefreshToken));
        return result.ToActionResult();
    }
}
