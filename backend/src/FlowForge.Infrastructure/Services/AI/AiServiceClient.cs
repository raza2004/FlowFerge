using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowForge.Application.AI.Assistant;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FlowForge.Infrastructure.Services.AI;

/// <summary>
/// Typed HttpClient for the Python AI service. Timeouts, retries and the circuit breaker live in the
/// HTTP pipeline (see <see cref="AiServiceRegistration"/>); this class turns whatever comes out of
/// it, including every kind of failure, into a Result so an AI outage never becomes a crash.
/// </summary>
public class AiServiceClient : IAiServiceClient
{
    public const string AskClient = "ai-service";
    public const string IngestClient = "ai-service-ingest";

    private const string Unavailable = "The AI assistant is temporarily unavailable. Please try again in a moment.";

    // The Python side speaks snake_case.
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<AiServiceClient> _logger;

    public AiServiceClient(IHttpClientFactory factory, ILogger<AiServiceClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<Result<AssistantAnswerDto>> AskAsync(string question, CancellationToken ct = default)
    {
        return await SendAsync(AskClient, () => Post("ask", question), async response =>
        {
            var wire = await response.Content.ReadFromJsonAsync<AnswerWire>(Json, ct);
            return wire == null
                ? Result.Failure<AssistantAnswerDto>(Error.ServiceUnavailable("AiService.BadResponse", Unavailable))
                : Result.Success(wire.ToDto());
        }, ct);
    }

    public async Task<Result<Stream>> StreamAskAsync(string question, CancellationToken ct = default)
    {
        // ResponseHeadersRead: the call completes once the first bytes arrive, so the 15 second limit
        // covers "did the AI service start answering?" and the body can then stream for as long as it needs.
        var result = await SendAsync(AskClient, () => Post("ask/stream", question), async response =>
            Result.Success<Stream>(await response.Content.ReadAsStreamAsync(ct)), ct, HttpCompletionOption.ResponseHeadersRead, keepResponseOpen: true);
        return result;
    }

    public async Task<Result<AssistantDocumentDto>> UploadDocumentAsync(string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        // Buffered in memory (max 25 MB, enforced by the handler) so the body can be re-read, and a
        // client-chosen document id means re-sending the same upload replaces rather than duplicates.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var documentId = Guid.NewGuid().ToString();

        HttpRequestMessage Build()
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            if (!string.IsNullOrWhiteSpace(contentType)) file.Headers.TryAddWithoutValidation("Content-Type", contentType);
            form.Add(file, "file", fileName);
            form.Add(new StringContent(documentId), "document_id");
            return new HttpRequestMessage(HttpMethod.Post, "documents") { Content = form };
        }

        return await SendAsync(IngestClient, Build, async response =>
        {
            var wire = await response.Content.ReadFromJsonAsync<DocumentWire>(Json, ct);
            return wire == null
                ? Result.Failure<AssistantDocumentDto>(Error.ServiceUnavailable("AiService.BadResponse", Unavailable))
                : Result.Success(wire.ToDto());
        }, ct);
    }

    public async Task<Result<IReadOnlyList<AssistantDocumentDto>>> ListDocumentsAsync(CancellationToken ct = default)
    {
        return await SendAsync(AskClient, () => new HttpRequestMessage(HttpMethod.Get, "documents"), async response =>
        {
            var wire = await response.Content.ReadFromJsonAsync<List<DocumentWire>>(Json, ct) ?? new();
            return Result.Success<IReadOnlyList<AssistantDocumentDto>>(wire.Select(w => w.ToDto()).ToList());
        }, ct);
    }

    public async Task<Result> DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        var result = await SendAsync(AskClient, () => new HttpRequestMessage(HttpMethod.Delete, $"documents/{Uri.EscapeDataString(documentId)}"),
            _ => Task.FromResult(Result.Success(true)), ct);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    private static HttpRequestMessage Post(string path, string question) =>
        new(HttpMethod.Post, path) { Content = JsonContent.Create(new { question }) };

    /// <summary>
    /// One place that sends a request and maps every outcome. A request is built per attempt, because an
    /// HttpRequestMessage can only be sent once and the resilience pipeline may send it several times.
    /// </summary>
    private async Task<Result<T>> SendAsync<T>(
        string clientName,
        Func<HttpRequestMessage> build,
        Func<HttpResponseMessage, Task<Result<T>>> read,
        CancellationToken ct,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead,
        bool keepResponseOpen = false)
    {
        HttpResponseMessage? response = null;
        try
        {
            var client = _factory.CreateClient(clientName);
            response = await client.SendAsync(build(), completion, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await read(response);
                if (!keepResponseOpen || result.IsFailure) response.Dispose();
                return result;
            }

            var failure = MapStatus(response.StatusCode);
            _logger.LogWarning("AI service returned {Status} for {Path}", (int)response.StatusCode, response.RequestMessage?.RequestUri?.AbsolutePath);
            response.Dispose();
            return Result.Failure<T>(failure);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            response?.Dispose();
            throw; // the caller (usually the browser) went away: not our failure to report
        }
        catch (Exception ex) when (ex is TimeoutRejectedException or TaskCanceledException or OperationCanceledException)
        {
            response?.Dispose();
            _logger.LogWarning("AI service call timed out");
            return Result.Failure<T>(Error.ServiceUnavailable("AiService.Timeout", "The AI assistant took too long to answer. Please try again."));
        }
        catch (BrokenCircuitException)
        {
            response?.Dispose();
            _logger.LogWarning("AI service circuit is open, failing fast");
            return Result.Failure<T>(Error.ServiceUnavailable("AiService.CircuitOpen", Unavailable));
        }
        catch (HttpRequestException ex)
        {
            response?.Dispose();
            _logger.LogWarning(ex, "AI service is unreachable");
            return Result.Failure<T>(Error.ServiceUnavailable("AiService.Unreachable", Unavailable));
        }
    }

    private static Error MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
            Error.Validation("AiService.InvalidRequest", "The AI assistant could not process that request."),
        HttpStatusCode.TooManyRequests =>
            Error.TooManyRequests("AiService.RateLimited", "The AI assistant is busy. Please wait a moment and try again."),
        // 401/403 mean the shared key is wrong: our misconfiguration, not the user's fault.
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            Error.ServiceUnavailable("AiService.Misconfigured", Unavailable),
        _ => Error.ServiceUnavailable("AiService.Failed", Unavailable)
    };

    // ----- wire shapes (what the Python service actually sends) -----

    private record CitationWire(string DocumentId, string DocumentName, int Page, string Content, double Score);
    private record UsageWire(string Model, int PromptTokens, int CompletionTokens, double CostUsd, int LatencyMs);
    private record AnswerWire(string Answer, List<CitationWire>? Sources, UsageWire? Usage, string? RetrievalMode)
    {
        public AssistantAnswerDto ToDto() => new(
            Answer,
            (Sources ?? new()).Select(s => new CitationDto(s.DocumentId, s.DocumentName, s.Page, s.Content, s.Score)).ToList(),
            Usage == null ? new AiUsageDto("", 0, 0, 0, 0) : new AiUsageDto(Usage.Model, Usage.PromptTokens, Usage.CompletionTokens, Usage.CostUsd, Usage.LatencyMs),
            RetrievalMode ?? "");
    }
    private record DocumentWire(string DocumentId, string FileName, int Pages, int Chunks)
    {
        public AssistantDocumentDto ToDto() => new(DocumentId, FileName, Pages, Chunks);
    }
}
