using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FlowForge.Infrastructure.Services.AI;

/// <summary>
/// Stamps every outgoing call to the AI service with who is asking and which request it belongs to.
/// It reads the claims from <see cref="IHttpContextAccessor"/> on purpose: HttpClient handlers live in
/// their own DI scope, so a scoped ICurrentUser injected here would not be the current request's user.
/// </summary>
public class AiServiceHeadersHandler : DelegatingHandler
{
    public const string CorrelationItemKey = "CorrelationId";
    public const string CorrelationHeader = "X-Correlation-Id";

    private readonly IHttpContextAccessor _http;
    private readonly AiServiceOptions _options;

    public AiServiceHeadersHandler(IHttpContextAccessor http, IOptions<AiServiceOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _http.HttpContext;
        var user = context?.User;

        var tenantId = user?.FindFirst("tenantId")?.Value;
        var userId = user?.FindFirst("sub")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var correlationId = context?.Items[CorrelationItemKey] as string ?? Guid.NewGuid().ToString();

        // Set, not Add: a retry re-sends the same request message and must not duplicate headers.
        Set(request, "X-Tenant-Id", tenantId);
        Set(request, "X-User-Id", userId);
        Set(request, CorrelationHeader, correlationId);
        Set(request, "X-Internal-Key", _options.InternalKey);

        return base.SendAsync(request, cancellationToken);
    }

    private static void Set(HttpRequestMessage request, string name, string? value)
    {
        request.Headers.Remove(name);
        if (!string.IsNullOrEmpty(value)) request.Headers.TryAddWithoutValidation(name, value);
    }
}
