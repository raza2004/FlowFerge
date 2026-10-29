using FlowForge.API.Common;
using FlowForge.Application.AI.Assistant;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Controllers;

/// <summary>
/// The browser's only door to the AI service. It never calls Python itself: the token is checked here,
/// the tenant comes from the token (never from the request), and the handlers decide who may do what.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/ai")]
public class AssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssistantController(IMediator mediator) => _mediator = mediator;

    public record AskRequest(string Question);

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        var result = await _mediator.Send(new AskAssistantQuery(request.Question ?? ""));
        return result.ToActionResult();
    }

    /// <summary>Relays the AI service's server-sent events straight to the browser as they are produced.</summary>
    [HttpPost("ask/stream")]
    public async Task StreamAsk([FromBody] AskRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new StreamAssistantQuery(request.Question ?? ""), ct);
        if (result.IsFailure)
        {
            await result.ToActionResult().ExecuteResultAsync(ControllerContext);
            return;
        }

        await using var upstream = result.Value;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // stop nginx from holding the stream back

        var buffer = new byte[4096];
        int read;
        try
        {
            // Flush after every chunk: that is what makes the answer appear word by word.
            while ((read = await upstream.ReadAsync(buffer, ct)) > 0)
            {
                await Response.Body.WriteAsync(buffer.AsMemory(0, read), ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // The browser closed the tab or pressed stop; disposing the upstream stream stops the AI work.
        }
        catch (IOException)
        {
            // The AI service dropped mid-answer. The client already has the partial text.
        }
    }

    [HttpGet("documents")]
    public async Task<IActionResult> ListDocuments()
        => (await _mediator.Send(new ListAssistantDocumentsQuery())).ToActionResult();

    [HttpPost("documents")]
    [RequestSizeLimit(AssistantDocumentRules.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { title = "Document.Empty", detail = "Choose a file to upload." });

        await using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadAssistantDocumentCommand(file.FileName, file.ContentType, file.Length, stream));
        return result.ToActionResult();
    }

    [HttpDelete("documents/{documentId}")]
    public async Task<IActionResult> DeleteDocument(string documentId)
        => (await _mediator.Send(new DeleteAssistantDocumentCommand(documentId))).ToActionResult();
}
