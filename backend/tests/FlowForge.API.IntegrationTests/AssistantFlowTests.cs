using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FlowForge.Application.AI.Assistant;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Shared.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowForge.API.IntegrationTests;

/// <summary>Records what the API asked the AI service to do, and lets a test make that service fail.</summary>
public class AiCallLog
{
    public ConcurrentBag<(Guid? Tenant, string Question)> Asks { get; } = new();
    public ConcurrentBag<(Guid? Tenant, string File)> Uploads { get; } = new();
    public volatile bool Down;
    public string StreamBody = "data: {\"type\":\"token\",\"text\":\"Hello\"}\n\ndata: {\"type\":\"done\"}\n\n";
}

/// <summary>Stands in for the Python service so the API's own behaviour (auth, tenant, roles, errors) is what's tested.</summary>
public class FakeAiServiceClient : IAiServiceClient
{
    private static readonly Error Outage = Error.ServiceUnavailable("AiService.Unreachable", "The AI assistant is temporarily unavailable. Please try again in a moment.");
    private readonly ICurrentUser _user;
    private readonly AiCallLog _log;

    public FakeAiServiceClient(ICurrentUser user, AiCallLog log) { _user = user; _log = log; }

    public Task<Result<AssistantAnswerDto>> AskAsync(string question, CancellationToken ct = default)
    {
        if (_log.Down) return Task.FromResult(Result.Failure<AssistantAnswerDto>(Outage));
        _log.Asks.Add((_user.TenantId, question));
        return Task.FromResult(Result.Success(new AssistantAnswerDto(
            $"Answer to: {question}",
            new[] { new CitationDto("d1", "roles.md", 2, "Owners can invite.", 0.9) },
            new AiUsageDto("fake", 10, 5, 0.0001, 12), "hybrid_rerank")));
    }

    public Task<Result<Stream>> StreamAskAsync(string question, CancellationToken ct = default) =>
        Task.FromResult(_log.Down
            ? Result.Failure<Stream>(Outage)
            : Result.Success<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(_log.StreamBody))));

    public async Task<Result<AssistantDocumentDto>> UploadDocumentAsync(string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        if (_log.Down) return Result.Failure<AssistantDocumentDto>(Outage);
        _log.Uploads.Add((_user.TenantId, fileName));
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        return Result.Success(new AssistantDocumentDto(Guid.NewGuid().ToString(), fileName, 1, 1));
    }

    public Task<Result<IReadOnlyList<AssistantDocumentDto>>> ListDocumentsAsync(CancellationToken ct = default) =>
        Task.FromResult(_log.Down
            ? Result.Failure<IReadOnlyList<AssistantDocumentDto>>(Outage)
            : Result.Success<IReadOnlyList<AssistantDocumentDto>>(new List<AssistantDocumentDto> { new("d1", "roles.md", 3, 7) }));

    public Task<Result> DeleteDocumentAsync(string documentId, CancellationToken ct = default) =>
        Task.FromResult(_log.Down ? Result.Failure(Outage) : Result.Success());
}

[Collection("Integration")]
public class AssistantFlowTests
{
    private const string Password = "Passw0rd123";
    private readonly CustomWebApplicationFactory _factory;
    private readonly AiCallLog _log = new();
    private readonly WebApplicationFactory<Program> _app;

    public AssistantFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiServiceClient>();
            services.AddSingleton(_log);
            services.AddScoped<IAiServiceClient, FakeAiServiceClient>();
        }));
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<AuthResponse> RegisterOwnerAsync()
    {
        var tag = Tag();
        var response = await _app.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            "Olive", "Owner", $"olive-{tag}@integration.test", Password, $"Team {tag}", $"team-{tag}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private HttpClient Authed(string token)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<AuthResponse> InviteMemberAsync(AuthResponse owner)
    {
        var email = $"member-{Tag()}@integration.test";
        var invite = await (await Authed(owner.AccessToken).PostAsJsonAsync("/api/v1/invitations", new { email, role = "Member" }))
            .Content.ReadFromJsonAsync<InvitationDto>();
        var token = invite!.InviteUrl.Split('/').Last();
        var register = await _app.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{token}/register",
            new RegisterWithInvitationRequest("Mia", "Member", Password));
        register.EnsureSuccessStatusCode();
        return (await register.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static MultipartFormDataContent File(string name, string text, string type = "text/markdown")
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        part.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(part, "file", name);
        return form;
    }

    [Fact]
    public async Task Asking_without_a_token_is_rejected_before_the_ai_service_is_ever_called()
    {
        var response = await _app.CreateClient().PostAsJsonAsync("/api/v1/ai/ask", new { question = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _log.Asks.Should().BeEmpty();
    }

    [Fact]
    public async Task A_signed_in_user_gets_an_answer_with_sources_and_the_tenant_comes_from_the_token()
    {
        var a = await RegisterOwnerAsync();
        var b = await RegisterOwnerAsync();

        var answer = await (await Authed(a.AccessToken).PostAsJsonAsync("/api/v1/ai/ask", new { question = "who can invite?" }))
            .Content.ReadFromJsonAsync<AssistantAnswerDto>();
        await Authed(b.AccessToken).PostAsJsonAsync("/api/v1/ai/ask", new { question = "other tenant question" });

        answer!.Answer.Should().Be("Answer to: who can invite?");
        answer.Sources.Should().ContainSingle().Which.DocumentName.Should().Be("roles.md");
        _log.Asks.Should().Contain(x => x.Tenant == a.Tenant!.Id && x.Question == "who can invite?");
        _log.Asks.Should().Contain(x => x.Tenant == b.Tenant!.Id, "each user is asked on behalf of their own tenant");
        a.Tenant!.Id.Should().NotBe(b.Tenant!.Id);
    }

    [Fact]
    public async Task A_tenant_id_in_the_request_cannot_be_used_to_reach_someone_elses_data()
    {
        var a = await RegisterOwnerAsync();
        var b = await RegisterOwnerAsync();
        var client = Authed(a.AccessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.Tenant!.Id.ToString());

        await client.PostAsJsonAsync("/api/v1/ai/ask", new { question = "spoof", tenantId = b.Tenant.Id });

        _log.Asks.Should().Contain(x => x.Question == "spoof" && x.Tenant == a.Tenant!.Id);
        _log.Asks.Should().NotContain(x => x.Question == "spoof" && x.Tenant == b.Tenant.Id);
    }

    [Fact]
    public async Task An_empty_question_is_a_400()
    {
        var owner = await RegisterOwnerAsync();

        var response = await Authed(owner.AccessToken).PostAsJsonAsync("/api/v1/ai/ask", new { question = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task When_the_ai_service_is_down_the_user_gets_a_clean_503_not_a_crash()
    {
        var owner = await RegisterOwnerAsync();
        _log.Down = true;

        var response = await Authed(owner.AccessToken).PostAsJsonAsync("/api/v1/ai/ask", new { question = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("temporarily unavailable").And.NotContain("Exception");

        // ...and the rest of FlowForge is unaffected.
        (await Authed(owner.AccessToken).GetAsync("/api/v1/projects")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_stream_endpoint_relays_server_sent_events_to_the_browser()
    {
        var owner = await RegisterOwnerAsync();

        var response = await Authed(owner.AccessToken).PostAsJsonAsync("/api/v1/ai/ask/stream", new { question = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"text\":\"Hello\"").And.Contain("\"type\":\"done\"");
    }

    [Fact]
    public async Task The_stream_endpoint_reports_an_outage_as_503_before_any_streaming_starts()
    {
        var owner = await RegisterOwnerAsync();
        _log.Down = true;

        var response = await Authed(owner.AccessToken).PostAsJsonAsync("/api/v1/ai/ask/stream", new { question = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Only_owners_and_admins_can_upload_documents_but_any_member_can_list_them()
    {
        var owner = await RegisterOwnerAsync();
        var member = await InviteMemberAsync(owner);

        var denied = await Authed(member.AccessToken).PostAsync("/api/v1/ai/documents", File("handbook.md", "hello"));
        var allowed = await Authed(owner.AccessToken).PostAsync("/api/v1/ai/documents", File("handbook.md", "hello"));
        var list = await Authed(member.AccessToken).GetAsync("/api/v1/ai/documents");

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        _log.Uploads.Should().ContainSingle(u => u.Tenant == owner.Tenant!.Id && u.File == "handbook.md");
    }

    [Fact]
    public async Task Members_cannot_delete_documents()
    {
        var owner = await RegisterOwnerAsync();
        var member = await InviteMemberAsync(owner);

        (await Authed(member.AccessToken).DeleteAsync("/api/v1/ai/documents/d1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Authed(owner.AccessToken).DeleteAsync("/api/v1/ai/documents/d1")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("photo.png")]
    public async Task Unsupported_file_types_are_rejected_without_calling_the_ai_service(string fileName)
    {
        var owner = await RegisterOwnerAsync();

        var response = await Authed(owner.AccessToken).PostAsync("/api/v1/ai/documents", File(fileName, "x", "application/octet-stream"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _log.Uploads.Should().NotContain(u => u.File == fileName);
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id_and_a_supplied_one_is_kept()
    {
        var client = _app.CreateClient();

        var generated = await client.GetAsync("/health");
        generated.Headers.GetValues("X-Correlation-Id").Single().Should().NotBeNullOrWhiteSpace();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "trace-abc_123");
        (await client.SendAsync(request)).Headers.GetValues("X-Correlation-Id").Single().Should().Be("trace-abc_123");
    }

    [Fact]
    public async Task A_suspicious_correlation_id_is_replaced_not_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "<script>alert(1)</script>");

        var response = await _app.CreateClient().SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().NotContain("<script>");
    }
}
