using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Infrastructure.Services.AI;
using FlowForge.Shared.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// Tests the typed client and its HTTP pipeline (timeout, retries, circuit breaker, headers) against a
/// stub in place of the Python service. No database or network: this is the part that has to keep the
/// API alive when the AI service is slow, broken or gone.
/// </summary>
public class AiServiceClientResilienceTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _behaviour;
        public int Calls;
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> behaviour) => _behaviour = behaviour;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref Calls);
            Requests.Add(request);
            return _behaviour(n, request, ct);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string AnswerJson =
        """{"answer":"Owners can invite [1].","sources":[{"document_id":"d1","document_name":"roles.md","page":2,"content":"Owners invite.","score":0.82}],"usage":{"model":"m","prompt_tokens":10,"completion_tokens":5,"cost_usd":0.0001,"latency_ms":120},"retrieval_mode":"hybrid_rerank"}""";

    private static IAiServiceClient Build(StubHandler stub, Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AiService:BaseUrl"] = "http://ai.test",
            ["AiService:InternalKey"] = "shared-secret",
            ["AiService:TimeoutSeconds"] = "6",
            ["AiService:MaxRetries"] = "2"
        };
        if (overrides != null) foreach (var kv in overrides) settings[kv.Key] = kv.Value;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("tenantId", Tenant), new Claim("sub", "user-42")
            }, "test"))
        };
        http.Items[AiServiceHeadersHandler.CorrelationItemKey] = "corr-123";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddAiServiceClient(config);
        // Swap the network for the stub, keeping the real handlers and resilience pipeline around it.
        services.AddHttpClient(AiServiceClient.AskClient).ConfigurePrimaryHttpMessageHandler(() => stub);
        services.AddHttpClient(AiServiceClient.IngestClient).ConfigurePrimaryHttpMessageHandler(() => stub);

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IAiServiceClient>();
    }

    [Fact]
    public async Task Maps_a_successful_answer_and_its_citations()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, AnswerJson)));

        var result = await Build(stub).AskAsync("who can invite?");

        result.IsSuccess.Should().BeTrue();
        result.Value.Answer.Should().Contain("Owners can invite");
        result.Value.Sources.Should().ContainSingle().Which.Should().Match<FlowForge.Application.AI.Assistant.CitationDto>(
            c => c.DocumentName == "roles.md" && c.Page == 2);
        result.Value.Usage.PromptTokens.Should().Be(10);
        result.Value.RetrievalMode.Should().Be("hybrid_rerank");
    }

    [Fact]
    public async Task Sends_tenant_user_correlation_and_internal_key_headers()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, AnswerJson)));

        await Build(stub).AskAsync("hello");

        var request = stub.Requests.Single();
        request.RequestUri!.AbsolutePath.Should().Be("/ask");
        request.Headers.GetValues("X-Tenant-Id").Should().ContainSingle().Which.Should().Be(Tenant);
        request.Headers.GetValues("X-User-Id").Should().ContainSingle().Which.Should().Be("user-42");
        request.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be("corr-123");
        request.Headers.GetValues("X-Internal-Key").Should().ContainSingle().Which.Should().Be("shared-secret");
    }

    [Fact]
    public async Task Retries_a_failing_service_twice_then_returns_a_friendly_error()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, """{"detail":"down"}""")));

        var result = await Build(stub).AskAsync("hello");

        stub.Calls.Should().Be(3, "one attempt plus two retries");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.ServiceUnavailable);
        result.Error.Message.Should().Contain("temporarily unavailable");
    }

    [Fact]
    public async Task A_retry_that_succeeds_hides_the_blip_from_the_user()
    {
        var stub = new StubHandler((n, _, _) => Task.FromResult(n == 1
            ? Json(HttpStatusCode.BadGateway, "{}")
            : Json(HttpStatusCode.OK, AnswerJson)));

        var result = await Build(stub).AskAsync("hello");

        result.IsSuccess.Should().BeTrue();
        stub.Calls.Should().Be(2);
    }

    [Fact]
    public async Task A_stopped_service_is_reported_as_unavailable_not_thrown()
    {
        var stub = new StubHandler((_, _, _) => throw new HttpRequestException("Connection refused"));

        var result = await Build(stub).AskAsync("hello");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.ServiceUnavailable);
        result.Error.Code.Should().Be("AiService.Unreachable");
    }

    [Fact]
    public async Task A_hanging_service_gives_up_within_the_time_limit()
    {
        var stub = new StubHandler(async (_, _, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(60), ct); // never answers
            return Json(HttpStatusCode.OK, AnswerJson);
        });
        var client = Build(stub, new() { ["AiService:TimeoutSeconds"] = "4", ["AiService:MaxRetries"] = "0" });

        var clock = Stopwatch.StartNew();
        var result = await client.AskAsync("hello");
        clock.Stop();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AiService.Timeout");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task A_slow_answer_is_not_retried_because_that_would_run_the_same_expensive_work_twice()
    {
        var stub = new StubHandler(async (_, _, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
            return Json(HttpStatusCode.OK, AnswerJson);
        });
        var client = Build(stub, new() { ["AiService:TimeoutSeconds"] = "3" });

        var result = await client.AskAsync("hello");

        result.Error.Code.Should().Be("AiService.Timeout");
        stub.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Bad_requests_are_not_retried()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.UnprocessableEntity, "{}")));

        var result = await Build(stub).AskAsync("hello");

        stub.Calls.Should().Be(1, "retrying the same bad request cannot help");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task A_rate_limited_provider_is_not_retried_and_does_not_trip_the_circuit()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.TooManyRequests, "{}")));
        var client = Build(stub);

        var first = await client.AskAsync("hello");
        stub.Calls.Should().Be(1, "retrying a 429 only adds load to a provider that is already limiting us");
        first.Error.Type.Should().Be(ErrorType.TooManyRequests);

        for (var i = 0; i < 8; i++) await client.AskAsync("hello");
        (await client.AskAsync("hello")).Error.Code.Should().Be("AiService.RateLimited", "429s mean busy, not broken, so the circuit stays closed");
    }

    [Fact]
    public async Task Circuit_breaker_opens_after_repeated_failures_and_stops_calling_the_service()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.InternalServerError, "{}")));
        var client = Build(stub, new() { ["AiService:MaxRetries"] = "0" });

        for (var i = 0; i < 6; i++) await client.AskAsync("hello");
        var callsWhenOpen = stub.Calls;

        var next = await client.AskAsync("hello");

        next.IsFailure.Should().BeTrue();
        next.Error.Code.Should().Be("AiService.CircuitOpen");
        stub.Calls.Should().Be(callsWhenOpen, "an open circuit fails fast without touching the broken service");
    }

    [Fact]
    public async Task A_wrong_shared_key_is_reported_as_our_misconfiguration_not_the_users()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, "{}")));

        var result = await Build(stub).AskAsync("hello");

        result.Error.Type.Should().Be(ErrorType.ServiceUnavailable);
        result.Error.Code.Should().Be("AiService.Misconfigured");
        result.Error.Message.Should().NotContain("key", "internals are not leaked to users");
    }

    [Fact]
    public async Task Stream_returns_the_event_stream_as_it_arrives()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: {\"type\":\"token\",\"text\":\"Hi\"}\n\n", Encoding.UTF8, "text/event-stream")
        }));

        var result = await Build(stub).StreamAskAsync("hello");

        result.IsSuccess.Should().BeTrue();
        using var reader = new StreamReader(result.Value);
        (await reader.ReadToEndAsync()).Should().Contain("\"text\":\"Hi\"");
        stub.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/ask/stream");
    }

    [Fact]
    public async Task Upload_is_sent_once_as_multipart_with_a_client_chosen_document_id_and_never_auto_retried()
    {
        var stub = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{}")));

        var result = await Build(stub).UploadDocumentAsync("notes.md", "text/markdown", new MemoryStream("hello"u8.ToArray()));

        stub.Calls.Should().Be(1, "ingestion is slow and a duplicate large upload is worse than one clear error");
        result.IsFailure.Should().BeTrue();
        var body = await stub.Requests.Single().Content!.ReadAsStringAsync();
        body.Should().Contain("filename=notes.md").And.Contain("document_id");
    }

    [Fact]
    public async Task An_unconfigured_ai_service_fails_cleanly_instead_of_crashing_startup()
    {
        var stub = new StubHandler((_, _, _) => throw new HttpRequestException("name not resolved"));
        var client = Build(stub, new() { ["AiService:BaseUrl"] = "" });

        var result = await client.AskAsync("hello");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.ServiceUnavailable);
    }
}
