using FlowForge.API.Common;
using FlowForge.Application.AI.Commands;
using FlowForge.Application.AI.DTOs;
using FlowForge.Application.AI.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class AiController : ControllerBase
{
    private readonly IMediator _mediator;
    public AiController(IMediator mediator) => _mediator = mediator;

    [HttpGet("tasks/{taskId:guid}/ai/breakdown")]
    public async Task<IActionResult> SuggestBreakdown(Guid taskId)
    {
        var result = await _mediator.Send(new SuggestTaskBreakdownQuery(taskId));
        return result.ToActionResult();
    }

    [HttpPost("tasks/{taskId:guid}/ai/breakdown/apply")]
    public async Task<IActionResult> ApplyBreakdown(Guid taskId, [FromBody] ApplyTaskBreakdownRequest req)
    {
        var result = await _mediator.Send(new ApplyTaskBreakdownCommand(taskId, req.SubtaskTitles));
        return result.ToActionResult();
    }

    [HttpGet("tasks/{taskId:guid}/ai/suggest-assignee")]
    public async Task<IActionResult> SuggestAssignee(Guid taskId)
    {
        var result = await _mediator.Send(new SuggestAssigneeQuery(taskId));
        return result.ToActionResult();
    }

    [HttpGet("projects/{projectId:guid}/ai/summary")]
    public async Task<IActionResult> ProjectSummary(Guid projectId)
    {
        var result = await _mediator.Send(new GetProjectAiSummaryQuery(projectId));
        return result.ToActionResult();
    }
}
