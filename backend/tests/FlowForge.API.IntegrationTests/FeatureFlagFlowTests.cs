using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FlowForge.Application.Features;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class FeatureFlagFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public FeatureFlagFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<AuthResponse> RegisterAsync(string name) =>
        (await (await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            name, "User", $"{name.ToLowerInvariant()}-{Tag()}@integration.test", "Passw0rd123", $"{name} Co {Tag()}", $"{name.ToLowerInvariant()}-{Tag()}")))
            .Content.ReadFromJsonAsync<AuthResponse>())!;

    private HttpClient Authed(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Whoever registers first in an empty database becomes system admin. Other test classes share this
    /// database, so rather than assume we're first, promote the account directly - what's under test is
    /// the flag behavior, not the bootstrap rule (covered by the auth tests).
    /// </summary>
    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await RegisterAsync("Admin");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowForge.Infrastructure.Persistence.FlowForgeDbContext>();
        var user = await db.Users.FindAsync(admin.User.Id);
        user!.PromoteToSystemAdmin();
        await db.SaveChangesAsync();

        // Admin status travels in the token, so sign in again to get one that carries it.
        var login = (await (await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(admin.User.Email, "Passw0rd123"))).Content.ReadFromJsonAsync<AuthResponse>())!;
        return Authed(login.AccessToken);
    }

    private async Task<(HttpClient Client, AuthResponse Auth, ProjectDto Project, BoardDto Board, TaskCardDto Task)> WorkspaceAsync()
    {
        var auth = await RegisterAsync("Member");
        var client = Authed(auth.AccessToken);
        var project = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Flags", key = "FLG" + Tag()[..4].ToUpperInvariant(),
            description = (string?)null, color = (string?)null, visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var board = (await (await client.GetAsync($"/api/v1/projects/{project.Id}/boards")).Content.ReadFromJsonAsync<List<BoardDto>>())![0];
        var task = (await (await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project.Id, boardId = board.Id, listId = board.Lists[0].Id,
            title = "Gated", description = (string?)null, type = 0, priority = 2
        })).Content.ReadFromJsonAsync<TaskCardDto>())!;
        return (client, auth, project, board, task);
    }

    private static MultipartFormDataContent File(string name) =>
        new() { { new ByteArrayContent(Encoding.UTF8.GetBytes("data")), "file", name } };

    [Fact]
    public async Task EveryDefinedFeature_IsSeededAndOnByDefault()
    {
        var admin = await AdminClientAsync();

        var flags = await (await admin.GetAsync("/api/v1/admin/features")).Content.ReadFromJsonAsync<List<FeatureFlagDto>>();

        flags!.Select(f => f.Key).Should().Contain(FeatureKeys.All.Select(d => d.Key));
        flags.Where(f => FeatureKeys.All.Any(d => d.Key == f.Key)).Should().OnlyContain(f => f.IsEnabled,
            "new features start switched on, and the other tests here always restore the global switch");
    }

    [Fact]
    public async Task OrdinaryUsers_CannotSeeOrChangeFlags()
    {
        var (client, _, _, _, _) = await WorkspaceAsync();

        (await client.GetAsync("/api/v1/admin/features")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.AiAssistant}", new { enabled = false }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SwitchingAFeatureOffForOneWorkspace_BlocksItThereOnly()
    {
        var admin = await AdminClientAsync();
        var (blocked, blockedAuth, _, blockedBoard, blockedTask) = await WorkspaceAsync();
        var (allowed, _, _, allowedBoard, allowedTask) = await WorkspaceAsync();

        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Attachments}/tenants/{blockedAuth.Tenant!.Id}",
            new { enabled = false })).EnsureSuccessStatusCode();

        var denied = await blocked.PostAsync($"/api/v1/tasks/{blockedTask.Id}/attachments?boardId={blockedBoard.Id}", File("a.txt"));
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.Content.ReadAsStringAsync()).Should().Contain("Feature.Disabled").And.Contain("turned off");

        (await allowed.PostAsync($"/api/v1/tasks/{allowedTask.Id}/attachments?boardId={allowedBoard.Id}", File("a.txt")))
            .EnsureSuccessStatusCode();

        var features = await (await blocked.GetAsync("/api/v1/features")).Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        features![FeatureKeys.Attachments].Should().BeFalse();
        features[FeatureKeys.Sprints].Should().BeTrue();

        // Removing the override restores the global setting (on).
        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Attachments}/tenants/{blockedAuth.Tenant.Id}",
            new { enabled = (bool?)null })).EnsureSuccessStatusCode();
        (await blocked.PostAsync($"/api/v1/tasks/{blockedTask.Id}/attachments?boardId={blockedBoard.Id}", File("a.txt")))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AWorkspaceOverrideBeatsTheGlobalSetting_InBothDirections()
    {
        var admin = await AdminClientAsync();
        var (member, auth, project, _, _) = await WorkspaceAsync();
        var sprint = new { name = "S1", goal = (string?)null, startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(7) };

        try
        {
            (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Sprints}", new { enabled = false })).EnsureSuccessStatusCode();
            (await member.PostAsJsonAsync($"/api/v1/projects/{project.Id}/sprints", sprint)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Globally off, but this workspace is explicitly allowed.
            (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Sprints}/tenants/{auth.Tenant!.Id}", new { enabled = true })).EnsureSuccessStatusCode();
            (await member.PostAsJsonAsync($"/api/v1/projects/{project.Id}/sprints", sprint)).EnsureSuccessStatusCode();
        }
        finally
        {
            // The flags are shared by every test in this database, so always put the global switch back.
            (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Sprints}", new { enabled = true })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task ExistingContent_StaysReadableWhenAFeatureIsOff()
    {
        var admin = await AdminClientAsync();
        var (member, auth, _, board, task) = await WorkspaceAsync();
        var upload = await member.PostAsync($"/api/v1/tasks/{task.Id}/attachments?boardId={board.Id}", File("keep.txt"));
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;

        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Attachments}/tenants/{auth.Tenant!.Id}", new { enabled = false }))
            .EnsureSuccessStatusCode();

        (await member.GetAsync($"/api/v1/attachments/{attachment.Id}/content")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TurningOffTheAiAssistant_BlocksEveryAiEndpoint()
    {
        var admin = await AdminClientAsync();
        var (member, auth, project, _, task) = await WorkspaceAsync();
        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.AiAssistant}/tenants/{auth.Tenant!.Id}", new { enabled = false }))
            .EnsureSuccessStatusCode();

        foreach (var url in new[]
        {
            $"/api/v1/tasks/{task.Id}/ai/breakdown",
            $"/api/v1/tasks/{task.Id}/ai/suggest-assignee",
            $"/api/v1/projects/{project.Id}/ai/summary",
            $"/api/v1/projects/{project.Id}/ai/blockers"
        })
        {
            (await member.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, url);
        }
    }

    [Fact]
    public async Task UnknownFlagsAndWorkspaces_AreRejected()
    {
        var admin = await AdminClientAsync();

        (await admin.PutAsJsonAsync("/api/v1/admin/features/no-such-feature", new { enabled = false })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureKeys.Sprints}/tenants/{Guid.NewGuid()}", new { enabled = false }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
