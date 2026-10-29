using FlowForge.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace FlowForge.Infrastructure.Services.AI;

public static class AiServiceRegistration
{
    public static IServiceCollection AddAiServiceClient(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AiServiceOptions>(config.GetSection(AiServiceOptions.Section));
        var options = config.GetSection(AiServiceOptions.Section).Get<AiServiceOptions>() ?? new AiServiceOptions();

        // A placeholder host keeps start-up working when the AI service is not configured; calls then fail
        // like any other outage (friendly 503) instead of the API refusing to boot.
        var baseAddress = new Uri(options.IsConfigured ? options.BaseUrl.TrimEnd('/') + "/" : "http://ai-service.not-configured/");

        services.AddHttpContextAccessor();
        services.AddTransient<AiServiceHeadersHandler>();

        // One attempt may use the whole budget: an answer is an expensive model call, and cutting it short to
        // retry would only run the same expensive work twice at the same time.
        var attempt = TimeSpan.FromSeconds(options.TimeoutSeconds);

        // Questions: 15 s overall, up to 2 retries with exponential backoff, and a circuit breaker that
        // stops hammering the AI service once it is clearly down.
        services.AddHttpClient(AiServiceClient.AskClient, c => c.BaseAddress = baseAddress)
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler)
            .AddHttpMessageHandler<AiServiceHeadersHandler>()
            .AddStandardResilienceHandler(o =>
            {
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                o.AttemptTimeout.Timeout = attempt;
                if (options.MaxRetries > 0) o.Retry.MaxRetryAttempts = options.MaxRetries;
                else DisableRetries(o.Retry);
                // Retry quick failures (network errors, 5xx) but NOT timeouts or 429: a rate limited provider only
                // recovers if we back off, and a timed out call is still running on the AI service, so a retry
                // would just add a second copy of the same expensive work.
                o.Retry.ShouldHandle = args => ValueTask.FromResult(IsOutage(args.Outcome) && !IsTimeout(args.Outcome));
                o.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(IsOutage(args.Outcome));
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.Retry.Delay = TimeSpan.FromMilliseconds(300);
                o.CircuitBreaker.FailureRatio = 0.5;
                o.CircuitBreaker.MinimumThroughput = 5;
                o.CircuitBreaker.SamplingDuration = attempt * 2 + TimeSpan.FromSeconds(10);
                o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });

        // Document ingestion embeds every chunk, so it needs minutes, not seconds, and is never retried
        // automatically: a second copy of a large upload is worse than one clear error.
        var ingest = TimeSpan.FromSeconds(options.IngestTimeoutSeconds);
        services.AddHttpClient(AiServiceClient.IngestClient, c => c.BaseAddress = baseAddress)
            .ConfigurePrimaryHttpMessageHandler(PrimaryHandler)
            .AddHttpMessageHandler<AiServiceHeadersHandler>()
            .AddStandardResilienceHandler(o =>
            {
                o.TotalRequestTimeout.Timeout = ingest;
                o.AttemptTimeout.Timeout = ingest;
                DisableRetries(o.Retry);
                o.CircuitBreaker.SamplingDuration = ingest * 2 + TimeSpan.FromSeconds(10);
                o.CircuitBreaker.MinimumThroughput = 5;
            });

        services.AddScoped<IAiServiceClient, AiServiceClient>();
        return services;
    }

    // A stopped service should be noticed in seconds, not after the OS gives up on the connection (Windows can
    // take over ten seconds to report a refused localhost connection). The limit covers every address tried: on
    // Windows "localhost" resolves to IPv6 first, and a service listening only on IPv4 is reached on the second
    // attempt, so this must be comfortably more than one refused connection (about 2 s). Prefer 127.0.0.1 in
    // configuration. Connections are also recycled every few minutes so a restarted service is picked up.
    private static HttpMessageHandler PrimaryHandler() => new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    };

    private static bool IsTimeout(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is Polly.Timeout.TimeoutRejectedException or TimeoutException;

    private static bool IsOutage(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or Polly.Timeout.TimeoutRejectedException or TimeoutException
        || (outcome.Result is { } r && (int)r.StatusCode >= 500);

    /// <summary>The library requires at least one retry attempt, so "no retries" means a retry that never decides to run.</summary>
    private static void DisableRetries(HttpRetryStrategyOptions retry)
    {
        retry.MaxRetryAttempts = 1;
        retry.ShouldHandle = _ => PredicateResult.False();
    }
}
