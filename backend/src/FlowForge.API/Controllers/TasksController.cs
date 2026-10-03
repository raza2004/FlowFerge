using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using FlowForge.API.Common;
using FlowForge.API.Hubs;
using FlowForge.Application.Projects.Commands;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Application.Projects.Queries;

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

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignTaskBody body,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new AssignTaskCommand(id, body.AssigneeId));
        if (result.IsSuccess && body.BoardId.HasValue)
        {
            await hub.Clients.Group($"board-{body.BoardId}").SendAsync("TaskAssigned", new
            {
                taskId = id,
                assigneeId = body.AssigneeId
            });
        }
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetTaskDetailQuery(id));
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTaskRequest body,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new UpdateTaskCommand(
            id, body.Title, body.Description, body.Type, body.Priority,
            body.DueDate, body.EstimatedHours, body.StoryPoints));
        if (result.IsSuccess) await BroadcastTaskChanged(hub, body.BoardId, id);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] Guid? boardId,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new DeleteTaskCommand(id));
        if (result.IsSuccess && boardId.HasValue)
            await hub.Clients.Group($"board-{boardId}").SendAsync("TaskDeleted", new { taskId = id });
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(
        Guid id,
        [FromBody] AddCommentRequest body,
        [FromQuery] Guid? boardId,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new AddCommentCommand(id, body.Content, body.MentionedUserIds));
        if (result.IsSuccess) await BroadcastTaskChanged(hub, boardId, id);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/subtasks")]
    public async Task<IActionResult> CreateSubtask(
        Guid id,
        [FromBody] CreateSubtaskRequest body,
        [FromQuery] Guid? boardId,
        [FromServices] IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new CreateSubtaskCommand(id, body.Title));
        if (result.IsSuccess) await BroadcastTaskChanged(hub, boardId, id);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/labels/{labelId:guid}")]
    public Task<IActionResult> AttachLabel(Guid id, Guid labelId, [FromQuery] Guid? boardId, [FromServices] IHubContext<BoardHub> hub) =>
        SetLabel(id, labelId, attach: true, boardId, hub);

    [HttpDelete("{id:guid}/labels/{labelId:guid}")]
    public Task<IActionResult> DetachLabel(Guid id, Guid labelId, [FromQuery] Guid? boardId, [FromServices] IHubContext<BoardHub> hub) =>
        SetLabel(id, labelId, attach: false, boardId, hub);

    [HttpPost("{id:guid}/time")]
    public async Task<IActionResult> LogTime(Guid id, [FromBody] LogTimeRequest body)
    {
        var result = await _mediator.Send(new LogTimeCommand(id, body.Hours, body.WorkDate, body.Note));
        return result.ToActionResult();
    }

    [HttpDelete("time/{entryId:guid}")]
    public async Task<IActionResult> DeleteTimeEntry(Guid entryId)
    {
        var result = await _mediator.Send(new DeleteTimeEntryCommand(entryId));
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/watch")]
    public async Task<IActionResult> Watch(Guid id) =>
        (await _mediator.Send(new SetTaskWatchCommand(id, Watch: true))).ToActionResult();

    [HttpDelete("{id:guid}/watch")]
    public async Task<IActionResult> Unwatch(Guid id) =>
        (await _mediator.Send(new SetTaskWatchCommand(id, Watch: false))).ToActionResult();

    private async Task<IActionResult> SetLabel(Guid id, Guid labelId, bool attach, Guid? boardId, IHubContext<BoardHub> hub)
    {
        var result = await _mediator.Send(new SetTaskLabelCommand(id, labelId, attach));
        if (result.IsSuccess) await BroadcastTaskChanged(hub, boardId, id);
        return result.ToActionResult();
    }

    /// <summary>Tells everyone viewing the board that a card's contents changed so they re-fetch it.</summary>
    private static Task BroadcastTaskChanged(IHubContext<BoardHub> hub, Guid? boardId, Guid taskId) =>
        boardId.HasValue
            ? hub.Clients.Group($"board-{boardId}").SendAsync("TaskUpdated", new { taskId })
            : Task.CompletedTask;
}

public record MoveTaskBody(Guid NewListId, int NewPosition, Guid? BoardId);
public record AssignTaskBody(Guid AssigneeId, Guid? BoardId);
