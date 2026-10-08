using FlowForge.API.Common;
using FlowForge.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

[ApiController]
[Route("api/v1")]
public class FeaturesController : ControllerBase
{
    private readonly IMediator _mediator;
    public FeaturesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Which features are on for the caller's workspace.</summary>
    [Authorize]
    [HttpGet("features")]
    public async Task<IActionResult> MyFeatures() =>
        (await _mediator.Send(new GetMyFeaturesQuery())).ToActionResult();

    [Authorize(Policy = "SystemAdmin")]
    [HttpGet("admin/features")]
    public async Task<IActionResult> All() =>
        (await _mediator.Send(new GetFeatureFlagsQuery())).ToActionResult();

    [Authorize(Policy = "SystemAdmin")]
    [HttpPut("admin/features/{key}")]
    public async Task<IActionResult> SetGlobal(string key, [FromBody] SetFeatureFlagRequest body) =>
        (await _mediator.Send(new SetFeatureFlagCommand(key, body.Enabled))).ToActionResult();

    [Authorize(Policy = "SystemAdmin")]
    [HttpPut("admin/features/{key}/tenants/{tenantId:guid}")]
    public async Task<IActionResult> SetOverride(string key, Guid tenantId, [FromBody] SetFeatureOverrideRequest body) =>
        (await _mediator.Send(new SetFeatureOverrideCommand(key, tenantId, body.Enabled))).ToActionResult();
}
