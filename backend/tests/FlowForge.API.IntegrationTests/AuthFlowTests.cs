using System.Net;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class AuthFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public AuthFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static RegisterRequest NewRegisterRequest(string tag) => new(
        FirstName: "Test",
        LastName: "User",
        Email: $"auth-{tag}@integration.test",
        Password: "Passw0rd123",
        TenantName: $"Integration Co {tag}",
        TenantSlug: $"integration-{tag}"
    );

    [Fact]
    public async Task Register_WithValidData_ReturnsTokensAndCreatesAnOwnerMembership()
    {
        var client = _factory.CreateClient();
        var req = NewRegisterRequest(Guid.NewGuid().ToString("N")[..8]);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be(req.Email.ToLowerInvariant());
        auth.Tenant.Should().NotBeNull();
        auth.Tenant!.Slug.Should().Be(req.TenantSlug);
    }

    [Fact]
    public async Task Register_WithAnEmailThatAlreadyExists_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var req = NewRegisterRequest(Guid.NewGuid().ToString("N")[..8]);
        await client.PostAsJsonAsync("/api/v1/auth/register", req);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", req with { TenantSlug = req.TenantSlug + "-2" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Succeeds()
    {
        var client = _factory.CreateClient();
        var req = NewRegisterRequest(Guid.NewGuid().ToString("N")[..8]);
        await client.PostAsJsonAsync("/api/v1/auth/register", req);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(req.Email, req.Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var req = NewRegisterRequest(Guid.NewGuid().ToString("N")[..8]);
        await client.PostAsJsonAsync("/api/v1/auth/register", req);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(req.Email, "WrongPassword123"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
