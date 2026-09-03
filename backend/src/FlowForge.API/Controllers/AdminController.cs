using FlowForge.API.Common;
using FlowForge.Application.Admin.Commands;
using FlowForge.Application.Admin.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

/// <summary>
/// Backs the separate admin panel (system-wide tenant/user management, audit logs).
/// Gated at two layers: the "SystemAdmin" policy here (401/403 before a request even
/// reaches MediatR), and every handler re-checks ICurrentUser.IsSystemAdmin itself -
/// the same defense-in-depth the rest of the app already uses for tenant scoping.
/// </summary>
[ApiController]
[Authorize(Policy = "SystemAdmin")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var result = await _mediator.Send(new GetAdminStatsQuery());
        return result.ToActionResult();
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants()
    {
        var result = await _mediator.Send(new GetAdminTenantsQuery());
        return result.ToActionResult();
    }

    [HttpPost("tenants/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendTenant(Guid id, [FromBody] SuspendTenantRequest req)
    {
        var result = await _mediator.Send(new SuspendTenantCommand(id, req.Reason));
        return result.ToActionResult();
    }

    [HttpPost("tenants/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateTenant(Guid id)
    {
        var result = await _mediator.Send(new ReactivateTenantCommand(id));
        return result.ToActionResult();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var result = await _mediator.Send(new GetAdminUsersQuery());
        return result.ToActionResult();
    }

    [HttpPost("users/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id)
    {
        var result = await _mediator.Send(new SuspendUserCommand(id));
        return result.ToActionResult();
    }

    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateUser(Guid id)
    {
        var result = await _mediator.Send(new ReactivateUserCommand(id));
        return result.ToActionResult();
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs()
    {
        var result = await _mediator.Send(new GetAdminAuditLogsQuery());
        return result.ToActionResult();
    }
}

public record SuspendTenantRequest(string Reason);
