using System.Security.Claims;
using FlowForge.Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace FlowForge.Infrastructure.Services.Auth;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue("email") ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public Guid? TenantId
    {
        get
        {
            var tid = Principal?.FindFirstValue("tenantId");
            return Guid.TryParse(tid, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public bool IsSystemAdmin => Principal?.FindFirstValue("isSystemAdmin") == "true";

    public string? IpAddress => _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    public string? UserAgent => _http.HttpContext?.Request?.Headers.UserAgent.ToString();
}
