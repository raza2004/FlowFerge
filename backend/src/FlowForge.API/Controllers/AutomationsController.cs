using FlowForge.API.Common;
using FlowForge.Application.Workflows.Commands;
using FlowForge.Application.Workflows.DTOs;
using FlowForge.Application.Workflows.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class AutomationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AutomationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("projects/{projectId:guid}/automations")]
    public async Task<IActionResult> GetForProject(Guid projectId)
    {
        var result = await _mediator.Send(new GetProjectAutomationRulesQuery(projectId));
        return result.ToActionResult();
    }

    [HttpPost("projects/{projectId:guid}/automations")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateAutomationRuleRequest req)
    {
        var result = await _mediator.Send(new CreateAutomationRuleCommand(
            projectId, req.Name, req.TriggerType, req.TriggerListId, req.ActionType, req.ActionUserId));
        return result.ToActionResult();
    }

    [HttpPost("automations/{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] ToggleAutomationRuleBody body)
    {
        var result = await _mediator.Send(new ToggleAutomationRuleCommand(id, body.Enabled));
        return result.ToActionResult();
    }

    [HttpDelete("automations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteAutomationRuleCommand(id));
        return result.ToActionResult();
    }
}

public record ToggleAutomationRuleBody(bool Enabled);
