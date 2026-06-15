using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlowForge.API.Common;
using FlowForge.Application.Projects.Queries;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var result = await _mediator.Send(new GetDashboardStatsQuery());
        return result.ToActionResult();
    }

    [HttpGet("my-tasks")]
    public async Task<IActionResult> MyTasks()
    {
        var result = await _mediator.Send(new GetMyTasksQuery());
        return result.ToActionResult();
    }
}
