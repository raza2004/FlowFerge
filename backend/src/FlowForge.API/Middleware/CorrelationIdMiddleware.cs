using FlowForge.Infrastructure.Services.AI;
using Serilog.Context;

namespace FlowForge.API.Middleware;

/// <summary>
/// Gives every request one correlation id: the caller's X-Correlation-Id if it sent one, otherwise a new
/// one. It is returned on the response, added to every log line, and forwarded to the AI service, so one
/// id finds the matching lines in both services' logs.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[AiServiceHeadersHandler.CorrelationHeader].FirstOrDefault();
        // Accept only a sane value: it ends up in logs and headers, so never trust arbitrary input.
        var id = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64 && incoming.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            ? incoming
            : Guid.NewGuid().ToString();

        context.Items[AiServiceHeadersHandler.CorrelationItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[AiServiceHeadersHandler.CorrelationHeader] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
            await _next(context);
    }
}
