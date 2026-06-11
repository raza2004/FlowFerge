using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using FlowForge.API.Common;
using FlowForge.API.Hubs;
using FlowForge.Application.Projects.Commands;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tasks")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;
    public TasksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskCommand cmd,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(cmd);
        if (result.IsSuccess)
        {
            await hub.Clients.Group($"board-{cmd.BoardId}").SendAsync("TaskCreated", result.Value);
        }
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> Move(
        Guid id,
        [FromBody] MoveTaskBody body,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new MoveTaskCommand(id, body.NewListId, body.NewPosition));
        if (result.IsSuccess && body.BoardId.HasValue)
        {
            await hub.Clients.Group($"board-{body.BoardId}").SendAsync("TaskMoved", new
            {
                taskId = id,
                newListId = body.NewListId,
                newPosition = body.NewPosition
            });
        }
        return result.ToActionResult();
    }
}

public record MoveTaskBody(Guid NewListId, int NewPosition, Guid? BoardId);
