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
[Route("api/v1/boards")]
public class BoardsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<BoardHub> _hub;

    public BoardsController(IMediator mediator, IHubContext<BoardHub> hub)
    {
        _mediator = mediator;
        _hub = hub;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetBoardByIdQuery(id));
        return result.ToActionResult();
    }

    [HttpPost("{boardId:guid}/lists")]
    public async Task<IActionResult> CreateList(Guid boardId, [FromBody] CreateListRequest body)
    {
        var result = await _mediator.Send(new CreateListCommand(boardId, body.Name, body.Color, body.WipLimit));
        if (result.IsSuccess) await BoardChanged(boardId);
        return result.ToActionResult();
    }

    [HttpPut("{boardId:guid}/lists/{listId:guid}")]
    public async Task<IActionResult> UpdateList(Guid boardId, Guid listId, [FromBody] UpdateListRequest body)
    {
        var result = await _mediator.Send(new UpdateListCommand(listId, body.Name, body.Color, body.WipLimit, body.IsDoneColumn));
        if (result.IsSuccess) await BoardChanged(boardId);
        return result.ToActionResult();
    }

    [HttpDelete("{boardId:guid}/lists/{listId:guid}")]
    public async Task<IActionResult> DeleteList(Guid boardId, Guid listId, [FromQuery] Guid? moveTasksTo)
    {
        var result = await _mediator.Send(new DeleteListCommand(listId, moveTasksTo));
        if (result.IsSuccess) await BoardChanged(boardId);
        return result.ToActionResult();
    }

    [HttpPut("{boardId:guid}/lists/order")]
    public async Task<IActionResult> ReorderLists(Guid boardId, [FromBody] ReorderListsRequest body)
    {
        var result = await _mediator.Send(new ReorderListsCommand(boardId, body.ListIds));
        if (result.IsSuccess) await BoardChanged(boardId);
        return result.ToActionResult();
    }

    private Task BoardChanged(Guid boardId) =>
        _hub.Clients.Group($"board-{boardId}").SendAsync("BoardChanged", new { boardId });
}
