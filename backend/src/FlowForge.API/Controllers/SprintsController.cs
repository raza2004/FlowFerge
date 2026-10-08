using FlowForge.API.Common;
using FlowForge.API.Hubs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Application.Projects.Sprints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class SprintsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<BoardHub> _hub;

    public SprintsController(IMediator mediator, IHubContext<BoardHub> hub)
    {
        _mediator = mediator;
        _hub = hub;
    }

    [HttpGet("projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> GetSprints(Guid projectId) =>
        (await _mediator.Send(new GetSprintsQuery(projectId))).ToActionResult();

    [HttpGet("projects/{projectId:guid}/backlog")]
    public async Task<IActionResult> GetBacklog(Guid projectId) =>
        (await _mediator.Send(new GetBacklogQuery(projectId))).ToActionResult();

    [HttpPost("projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateSprintRequest body) =>
        (await _mediator.Send(new CreateSprintCommand(projectId, body.Name, body.Goal, body.StartDate, body.EndDate))).ToActionResult();

    [HttpGet("sprints/{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id) =>
        (await _mediator.Send(new GetSprintDetailQuery(id))).ToActionResult();

    [HttpPut("sprints/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSprintRequest body) =>
        (await _mediator.Send(new UpdateSprintCommand(id, body.Name, body.Goal, body.StartDate, body.EndDate))).ToActionResult();

    [HttpPost("sprints/{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id) =>
        (await _mediator.Send(new StartSprintCommand(id))).ToActionResult();

    [HttpPost("sprints/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteSprintRequest body) =>
        (await _mediator.Send(new CompleteSprintCommand(id, body.RetrospectiveNotes, body.MoveIncompleteToSprintId))).ToActionResult();

    [HttpPost("sprints/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id) =>
        (await _mediator.Send(new CancelSprintCommand(id))).ToActionResult();

    [HttpPut("sprints/{id:guid}/retrospective")]
    public async Task<IActionResult> UpdateRetrospective(Guid id, [FromBody] UpdateRetrospectiveRequest body) =>
        (await _mediator.Send(new UpdateRetrospectiveCommand(id, body.Notes))).ToActionResult();

    [HttpPut("tasks/{taskId:guid}/sprint")]
    public async Task<IActionResult> AssignTask(Guid taskId, [FromBody] AssignSprintRequest body, [FromQuery] Guid? boardId)
    {
        var result = await _mediator.Send(new AssignTaskToSprintCommand(taskId, body.SprintId));
        if (result.IsSuccess && boardId.HasValue)
            await _hub.Clients.Group($"board-{boardId}").SendAsync("TaskUpdated", new { taskId });
        return result.ToActionResult();
    }
}
