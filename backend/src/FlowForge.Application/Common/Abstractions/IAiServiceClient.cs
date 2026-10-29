using FlowForge.Application.AI.Assistant;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Common.Abstractions;

/// <summary>
/// The only way the API talks to the Python AI service. The tenant, user and correlation id
/// travel with every call (added by the HTTP layer), so callers never pass them by hand and can't
/// forget them. Every method returns a Result: an outage becomes a ServiceUnavailable error,
/// never an exception that reaches the user.
/// </summary>
public interface IAiServiceClient
{
    Task<Result<AssistantAnswerDto>> AskAsync(string question, CancellationToken ct = default);

    /// <summary>Opens the server-sent-event stream. The caller owns (and must dispose) the stream.</summary>
    Task<Result<Stream>> StreamAskAsync(string question, CancellationToken ct = default);

    Task<Result<AssistantDocumentDto>> UploadDocumentAsync(string fileName, string contentType, Stream content, CancellationToken ct = default);

    Task<Result<IReadOnlyList<AssistantDocumentDto>>> ListDocumentsAsync(CancellationToken ct = default);

    Task<Result> DeleteDocumentAsync(string documentId, CancellationToken ct = default);
}
