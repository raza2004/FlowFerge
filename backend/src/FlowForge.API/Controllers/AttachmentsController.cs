using System.Net.Mime;
using FlowForge.API.Common;
using FlowForge.API.Hubs;
using FlowForge.Application.Projects.Attachments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace FlowForge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class AttachmentsController : ControllerBase
{
    // A little headroom over the file limit for the multipart envelope itself.
    private const long MaxRequestBytes = AttachmentRules.MaxFileSizeBytes + 1024 * 1024;

    private readonly IMediator _mediator;
    private readonly IHubContext<BoardHub> _hub;

    public AttachmentsController(IMediator mediator, IHubContext<BoardHub> hub)
    {
        _mediator = mediator;
        _hub = hub;
    }

    [HttpPost("tasks/{taskId:guid}/attachments")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Upload(Guid taskId, IFormFile? file, [FromQuery] Guid? boardId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Attachment.Missing", Detail = "Choose a file to upload", Status = 400 });

        await using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadAttachmentCommand(taskId, file.FileName, file.ContentType, file.Length, stream));

        if (result.IsSuccess && boardId.HasValue)
            await _hub.Clients.Group($"board-{boardId}").SendAsync("TaskUpdated", new { taskId });
        return result.ToActionResult();
    }

    /// <summary>
    /// Streams the file through the API (so access is checked like any other request). Only
    /// raster images are ever served inline; everything else is a download, so an uploaded
    /// HTML or SVG file can never run script on this origin.
    /// </summary>
    [HttpGet("attachments/{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id, [FromQuery] bool inline = false)
    {
        var result = await _mediator.Send(new GetAttachmentContentQuery(id));
        if (result.IsFailure) return result.ToActionResult();

        var file = result.Value;
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (inline && file.IsPreviewableImage)
        {
            Response.Headers.ContentDisposition = new ContentDisposition { Inline = true, FileName = file.FileName }.ToString();
            return File(file.Content, file.ContentType);
        }

        return File(file.Content, MediaTypeNames.Application.Octet, file.FileName);
    }

    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? boardId, [FromQuery] Guid? taskId)
    {
        var result = await _mediator.Send(new DeleteAttachmentCommand(id));
        if (result.IsSuccess && boardId.HasValue && taskId.HasValue)
            await _hub.Clients.Group($"board-{boardId}").SendAsync("TaskUpdated", new { taskId });
        return result.ToActionResult();
    }
}
