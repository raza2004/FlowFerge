using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FlowForge.API.RateLimiting;

public static class RateLimitingExtensions
{
    /// <summary>Policy for credential endpoints (login, register, refresh), far stricter than the global limit.</summary>
    public const string AuthPolicy = "auth";

    private static readonly string[] ExemptPrefixes = { "/hubs", "/health", "/swagger" };

    /// <summary>
    /// Two layers: a generous global limit per signed-in user (or per IP when anonymous) that
    /// stops runaway clients, and a strict per-IP limit on credential endpoints that makes
    /// password guessing impractical. Limits come from RateLimiting:* config, so tests and
    /// deployments can tune them, and RateLimiting:Enabled=false turns the whole thing off.
    ///
    /// Partitioning is by the direct peer address. Behind a reverse proxy that address is the
    /// proxy's, so configure forwarded headers (with your proxy as a known proxy) before relying on it.
    /// </summary>
    public static IServiceCollection AddFlowForgeRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        if (config.GetValue("RateLimiting:Enabled", true) == false) return services;

        var global = Window(config, "RateLimiting:Global", defaultPermits: 300);
        var auth = Window(config, "RateLimiting:Auth", defaultPermits: 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (ExemptPrefixes.Any(p => context.Request.Path.StartsWithSegments(p)))
                    return RateLimitPartition.GetNoLimiter("exempt");

                return RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => FixedWindow(global));
            });

            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{context.Connection.RemoteIpAddress}", _ => FixedWindow(auth)));

            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "RateLimit.Exceeded",
                    Detail = "Too many requests. Please wait a moment and try again."
                }), ct);
            };
        });

        return services;
    }

    private static string PartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId != null ? $"user:{userId}" : $"ip:{context.Connection.RemoteIpAddress}";
    }

    private record WindowSettings(int Permits, TimeSpan Window);

    private static WindowSettings Window(IConfiguration config, string section, int defaultPermits) => new(
        Math.Max(1, config.GetValue($"{section}:PermitLimit", defaultPermits)),
        TimeSpan.FromSeconds(Math.Max(1, config.GetValue($"{section}:WindowSeconds", 60))));

    private static FixedWindowRateLimiterOptions FixedWindow(WindowSettings s) => new()
    {
        PermitLimit = s.Permits,
        Window = s.Window,
        QueueLimit = 0,
        AutoReplenishment = true
    };
}
