using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Notifications.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Notifications.Enums;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class InvitationFlowTests
{
    private const string Password = "Passw0rd123";
    private readonly CustomWebApplicationFactory _factory;
    public InvitationFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private HttpClient Authed(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<AuthResponse> RegisterOwnerAsync(string tag, string firstName = "Olivia")
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            firstName, "Owner", $"{firstName.ToLowerInvariant()}-{tag}@integration.test", Password, $"Team {tag}", $"team-{tag}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<InvitationDto> InviteAsync(HttpClient owner, string email, string role = "Member")
    {
        var response = await owner.PostAsJsonAsync("/api/v1/invitations", new { email, role });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationDto>())!;
    }

    private static string TokenFrom(InvitationDto invitation) => invitation.InviteUrl.Split('/').Last();

    [Fact]
    public async Task NewPerson_RegistersFromInvite_AndLandsInTheInvitingWorkspace()
    {
        var tag = Tag();
        var owner = await RegisterOwnerAsync(tag);
        var ownerClient = Authed(owner.AccessToken);
        var inviteeEmail = $"newbie-{tag}@integration.test";

        var invitation = await InviteAsync(ownerClient, inviteeEmail);
        var token = TokenFrom(invitation);

        var preview = await (await _factory.CreateClient().GetAsync($"/api/v1/invitations/by-token/{token}"))
            .Content.ReadFromJsonAsync<InvitationPreviewDto>();
        preview!.Status.Should().Be("pending");
        preview.AccountExists.Should().BeFalse();
        preview.TenantName.Should().Be(owner.Tenant!.Name);

        var register = await _factory.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{token}/register",
            new RegisterWithInvitationRequest("Nina", "Newbie", Password));
        register.EnsureSuccessStatusCode();
        var invitee = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;

        invitee.Tenant!.Id.Should().Be(owner.Tenant.Id);
        invitee.User.Email.Should().Be(inviteeEmail);
        invitee.User.IsEmailVerified.Should().BeTrue();

        var members = await (await ownerClient.GetAsync("/api/v1/users/members")).Content.ReadFromJsonAsync<List<TenantMemberDto>>();
        members.Should().Contain(m => m.UserId == invitee.User.Id && m.Role == "Member");

        // A used invitation can't be used a second time.
        var reuse = await _factory.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{token}/register",
            new RegisterWithInvitationRequest("Someone", "Else", Password));
        reuse.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task ExistingUser_AcceptsInvite_ThenSwitchesWorkspaces_AndRefreshKeepsTheChoice()
    {
        var tag = Tag();
        var owner = await RegisterOwnerAsync(tag);
        var other = await RegisterOwnerAsync(Tag(), "Ethan");
        var invitation = await InviteAsync(Authed(owner.AccessToken), other.User.Email, "Admin");

        var accept = await Authed(other.AccessToken).PostAsync($"/api/v1/invitations/by-token/{TokenFrom(invitation)}/accept", null);
        accept.EnsureSuccessStatusCode();
        var joined = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;
        joined.Tenant!.Id.Should().Be(owner.Tenant!.Id, "accepting switches you into the workspace you joined");

        var workspaces = await (await Authed(joined.AccessToken).GetAsync("/api/v1/users/workspaces"))
            .Content.ReadFromJsonAsync<List<WorkspaceDto>>();
        workspaces.Should().HaveCount(2);
        workspaces!.Single(w => w.IsCurrent).TenantId.Should().Be(owner.Tenant.Id);

        // Switch back to their own workspace...
        var switched = (await (await Authed(joined.AccessToken).PostAsJsonAsync("/api/v1/users/workspaces/switch",
            new SwitchWorkspaceRequest(other.Tenant!.Id, joined.RefreshToken))).Content.ReadFromJsonAsync<AuthResponse>())!;
        switched.Tenant!.Id.Should().Be(other.Tenant.Id);

        // ...and a token refresh keeps them there instead of jumping to their first workspace.
        var refreshed = (await (await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(switched.RefreshToken))).Content.ReadFromJsonAsync<AuthResponse>())!;
        refreshed.Tenant!.Id.Should().Be(other.Tenant.Id);

        // The refresh token replaced during the switch is no longer usable.
        var stale = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(joined.RefreshToken));
        stale.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invite_CannotBeAcceptedByADifferentAccount()
    {
        var owner = await RegisterOwnerAsync(Tag());
        var stranger = await RegisterOwnerAsync(Tag(), "Sam");
        var invitation = await InviteAsync(Authed(owner.AccessToken), $"intended-{Tag()}@integration.test");

        var response = await Authed(stranger.AccessToken).PostAsync($"/api/v1/invitations/by-token/{TokenFrom(invitation)}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RevokedInvite_CannotBeUsed_AndMembersCannotInvite()
    {
        var tag = Tag();
        var owner = await RegisterOwnerAsync(tag);
        var ownerClient = Authed(owner.AccessToken);

        var revoked = await InviteAsync(ownerClient, $"revoked-{tag}@integration.test");
        (await ownerClient.DeleteAsync($"/api/v1/invitations/{revoked.Id}")).EnsureSuccessStatusCode();
        var useRevoked = await _factory.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{TokenFrom(revoked)}/register",
            new RegisterWithInvitationRequest("Rae", "Voked", Password));
        useRevoked.IsSuccessStatusCode.Should().BeFalse();

        var memberInvite = await InviteAsync(ownerClient, $"member-{tag}@integration.test");
        var member = (await (await _factory.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{TokenFrom(memberInvite)}/register",
            new RegisterWithInvitationRequest("Max", "Member", Password))).Content.ReadFromJsonAsync<AuthResponse>())!;

        var memberAttempt = await Authed(member.AccessToken).PostAsJsonAsync("/api/v1/invitations",
            new { email = $"x-{tag}@integration.test", role = "Member" });
        memberAttempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownerRoleAttempt = await ownerClient.PostAsJsonAsync("/api/v1/invitations",
            new { email = $"y-{tag}@integration.test", role = "Owner" });
        ownerRoleAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OwnerCanChangeRolesAndRemoveMembers_ButNotThemselves()
    {
        var tag = Tag();
        var owner = await RegisterOwnerAsync(tag);
        var ownerClient = Authed(owner.AccessToken);
        var invitation = await InviteAsync(ownerClient, $"teammate-{tag}@integration.test");
        var teammate = (await (await _factory.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{TokenFrom(invitation)}/register",
            new RegisterWithInvitationRequest("Tia", "Mate", Password))).Content.ReadFromJsonAsync<AuthResponse>())!;

        (await ownerClient.PutAsJsonAsync($"/api/v1/users/members/{teammate.User.Id}/role", new { role = "Manager" }))
            .EnsureSuccessStatusCode();
        var members = await (await ownerClient.GetAsync("/api/v1/users/members")).Content.ReadFromJsonAsync<List<TenantMemberDto>>();
        members!.Single(m => m.UserId == teammate.User.Id).Role.Should().Be("Manager");

        (await ownerClient.DeleteAsync($"/api/v1/users/members/{owner.User.Id}")).IsSuccessStatusCode.Should().BeFalse();

        (await ownerClient.DeleteAsync($"/api/v1/users/members/{teammate.User.Id}")).EnsureSuccessStatusCode();
        members = await (await ownerClient.GetAsync("/api/v1/users/members")).Content.ReadFromJsonAsync<List<TenantMemberDto>>();
        members.Should().NotContain(m => m.UserId == teammate.User.Id);

        // Their next refresh no longer lands them in the workspace they were removed from.
        var refreshed = (await (await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(teammate.RefreshToken))).Content.ReadFromJsonAsync<AuthResponse>())!;
        refreshed.Tenant.Should().BeNull();
    }

    [Fact]
    public async Task MentioningATeammateInAComment_SendsThemAMentionNotification()
    {
        var tag = Tag();
        var owner = await RegisterOwnerAsync(tag);
        var ownerClient = Authed(owner.AccessToken);
        var invitation = await InviteAsync(ownerClient, $"mentioned-{tag}@integration.test");
        var teammate = (await (await _factory.CreateClient().PostAsJsonAsync($"/api/v1/invitations/by-token/{TokenFrom(invitation)}/register",
            new RegisterWithInvitationRequest("Mia", "Mentioned", Password))).Content.ReadFromJsonAsync<AuthResponse>())!;

        var project = (await (await ownerClient.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Mentions", key = "MEN" + tag[..4].ToUpperInvariant(),
            description = (string?)null, color = (string?)null, visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var board = (await (await ownerClient.GetAsync($"/api/v1/projects/{project.Id}/boards")).Content.ReadFromJsonAsync<List<BoardDto>>())![0];
        var task = (await (await ownerClient.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project.Id, boardId = board.Id, listId = board.Lists[0].Id,
            title = "Review copy", description = (string?)null, type = 0, priority = 2
        })).Content.ReadFromJsonAsync<TaskCardDto>())!;

        (await ownerClient.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/comments?boardId={board.Id}",
            new { content = "@Mia Mentioned can you check this?", mentionedUserIds = new[] { teammate.User.Id } }))
            .EnsureSuccessStatusCode();

        var notifications = await (await Authed(teammate.AccessToken).GetAsync("/api/v1/notifications"))
            .Content.ReadFromJsonAsync<List<NotificationDto>>();
        notifications.Should().Contain(n => n.Type == NotificationType.Mentioned && n.RelatedTaskId == task.Id);
    }
}
