using System.Net;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class RateLimitingFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public RateLimitingFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient ClientWithLimits(int authPermits, int globalPermits) =>
        _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Auth:PermitLimit", authPermits.ToString());
            b.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
            b.UseSetting("RateLimiting:Global:PermitLimit", globalPermits.ToString());
        }).CreateClient();

    private static Task<HttpResponseMessage> BadLogin(HttpClient client) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@integration.test", "Wrong-Passw0rd"));

    [Fact]
    public async Task RepeatedLoginAttempts_AreCutOff_WithA429AndARetryHint()
    {
        var client = ClientWithLimits(authPermits: 3, globalPermits: 1000);

        for (var i = 0; i < 3; i++)
            (await BadLogin(client)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the first attempts reach the login handler");

        var blocked = await BadLogin(client);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.Should().NotBeNull();
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("RateLimit.Exceeded");
    }

    [Fact]
    public async Task OnlyCredentialEndpointsGetTheStrictLimit()
    {
        var client = ClientWithLimits(authPermits: 1, globalPermits: 1000);

        (await BadLogin(client)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await BadLogin(client)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Other endpoints are unaffected by the auth policy.
        (await client.GetAsync("/health")).IsSuccessStatusCode.Should().BeTrue();
        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheGlobalLimit_ProtectsOrdinaryEndpoints_ButNeverTheHealthCheck()
    {
        var client = ClientWithLimits(authPermits: 1000, globalPermits: 2);

        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        for (var i = 0; i < 5; i++)
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK, "monitoring must never be throttled");
    }
}
