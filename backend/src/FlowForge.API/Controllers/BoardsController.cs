using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlowForge.API.Common;
using FlowForge.Application.Projects.Queries;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/boards")]
public class BoardsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BoardsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetBoardByIdQuery(id));
        return result.ToActionResult();
    }
}
