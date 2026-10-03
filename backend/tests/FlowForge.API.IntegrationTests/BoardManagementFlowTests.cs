using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Application.Workflows.DTOs;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class BoardManagementFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public BoardManagementFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HttpClient Client, ProjectDto Project, BoardDto Board, Guid UserId)> SetupAsync()
    {
        var tag = Tag();
        var client = _factory.CreateClient();
        var auth = (await (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            "Board", "Builder", $"board-{tag}@integration.test", "Passw0rd123", $"Boards {tag}", $"boards-{tag}")))
            .Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var project = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Boards", key = "BRD" + tag[..4].ToUpperInvariant(),
            description = (string?)null, color = (string?)null, visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;

        return (client, project, await GetBoard(client, project.Id), auth.User.Id);
    }

    private static async Task<BoardDto> GetBoard(HttpClient client, Guid projectId) =>
        (await (await client.GetAsync($"/api/v1/projects/{projectId}/boards")).Content.ReadFromJsonAsync<List<BoardDto>>())![0];

    private static async Task<TaskCardDto> CreateTask(HttpClient client, Guid projectId, Guid boardId, Guid listId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId, boardId, listId, title, description = (string?)null, type = 0, priority = 2
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskCardDto>())!;
    }

    [Fact]
    public async Task NewTask_TakesItsListsStatus_AndGoesToTheBottom()
    {
        var (client, project, board, _) = await SetupAsync();
        var inProgress = board.Lists.Single(l => l.Name == "In Progress");

        var first = await CreateTask(client, project.Id, board.Id, inProgress.Id, "First");
        var second = await CreateTask(client, project.Id, board.Id, inProgress.Id, "Second");

        var detail = await (await client.GetAsync($"/api/v1/tasks/{second.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        detail!.Status.Should().Be("In Progress");

        var cards = (await GetBoard(client, project.Id)).Lists.Single(l => l.Id == inProgress.Id).Tasks;
        cards.Select(c => c.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task MovingACardToTheTop_KeepsThatOrderAfterReload()
    {
        var (client, project, board, _) = await SetupAsync();
        var todo = board.Lists.Single(l => l.Name == "To Do");
        var a = await CreateTask(client, project.Id, board.Id, todo.Id, "A");
        var b = await CreateTask(client, project.Id, board.Id, todo.Id, "B");
        var c = await CreateTask(client, project.Id, board.Id, todo.Id, "C");

        (await client.PostAsJsonAsync($"/api/v1/tasks/{c.Id}/move", new { newListId = todo.Id, newPosition = 0, boardId = board.Id }))
            .EnsureSuccessStatusCode();

        var cards = (await GetBoard(client, project.Id)).Lists.Single(l => l.Id == todo.Id).Tasks;
        cards.Select(x => x.Id).Should().Equal(c.Id, a.Id, b.Id);
        cards.Select(x => x.Position).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task Lists_CanBeCreatedRenamedReorderedAndMarkedDone()
    {
        var (client, project, board, _) = await SetupAsync();

        var review = (await (await client.PostAsJsonAsync($"/api/v1/boards/{board.Id}/lists",
            new { name = "Review", color = "#F59E0B", wipLimit = 3 })).Content.ReadFromJsonAsync<BoardListDto>())!;
        var task = await CreateTask(client, project.Id, board.Id, review.Id, "Needs review");

        (await client.PutAsJsonAsync($"/api/v1/boards/{board.Id}/lists/{review.Id}",
            new { name = "Shipped", color = "#0EA97C", wipLimit = (int?)null, isDoneColumn = true })).EnsureSuccessStatusCode();

        var detail = await (await client.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        detail!.Status.Should().Be("Shipped");
        detail.IsCompleted.Should().BeTrue("marking a list as done completes the tasks already in it");

        var reversed = (await GetBoard(client, project.Id)).Lists.Select(l => l.Id).Reverse().ToList();
        (await client.PutAsJsonAsync($"/api/v1/boards/{board.Id}/lists/order", new { listIds = reversed })).EnsureSuccessStatusCode();

        var updated = await GetBoard(client, project.Id);
        updated.Lists.Select(l => l.Id).Should().Equal(reversed);
        updated.Lists.First().Name.Should().Be("Shipped");
        updated.Lists.First().IsDoneColumn.Should().BeTrue();

        var badOrder = await client.PutAsJsonAsync($"/api/v1/boards/{board.Id}/lists/order", new { listIds = reversed.Take(2) });
        badOrder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeletingANonEmptyList_RequiresATarget_MovesTheTasks_AndRemovesItsAutomations()
    {
        var (client, project, board, userId) = await SetupAsync();
        var inProgress = board.Lists.Single(l => l.Name == "In Progress");
        var done = board.Lists.Single(l => l.Name == "Done");
        var task = await CreateTask(client, project.Id, board.Id, inProgress.Id, "Half done");

        (await client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/automations", new
        {
            name = "Watch in progress", triggerType = 0, triggerListId = inProgress.Id, actionType = 0, actionUserId = userId
        })).EnsureSuccessStatusCode();

        var withoutTarget = await client.DeleteAsync($"/api/v1/boards/{board.Id}/lists/{inProgress.Id}");
        withoutTarget.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.DeleteAsync($"/api/v1/boards/{board.Id}/lists/{inProgress.Id}?moveTasksTo={done.Id}")).EnsureSuccessStatusCode();

        var updated = await GetBoard(client, project.Id);
        updated.Lists.Should().NotContain(l => l.Id == inProgress.Id);
        updated.Lists.Single(l => l.Id == done.Id).Tasks.Should().Contain(t => t.Id == task.Id);
        updated.Lists.Select(l => l.Position).Should().Equal(Enumerable.Range(0, updated.Lists.Count));

        var rules = await (await client.GetAsync($"/api/v1/projects/{project.Id}/automations"))
            .Content.ReadFromJsonAsync<List<AutomationRuleDto>>();
        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task LoggedTime_AddsUp_AndDeletingAnEntryTakesItBackOff()
    {
        var (client, project, board, _) = await SetupAsync();
        var task = await CreateTask(client, project.Id, board.Id, board.Lists[0].Id, "Timed work");

        var first = (await (await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/time",
            new { hours = 2.0, workDate = (DateTime?)null, note = "Spike" })).Content.ReadFromJsonAsync<TimeEntryDto>())!;
        (await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/time",
            new { hours = 1.5, workDate = (DateTime?)null, note = (string?)null })).EnsureSuccessStatusCode();

        var detail = await (await client.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        detail!.ActualHours.Should().Be(3.5);
        detail.TimeEntries.Should().HaveCount(2);

        (await client.DeleteAsync($"/api/v1/tasks/time/{first.Id}")).EnsureSuccessStatusCode();
        detail = await (await client.GetAsync($"/api/v1/tasks/{task.Id}")).Content.ReadFromJsonAsync<TaskDetailDto>();
        detail!.ActualHours.Should().Be(1.5);

        var tooMuch = await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/time", new { hours = 25.0 });
        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatingATaskInAnotherProjectsList_IsRejected()
    {
        var (client, projectA, _, _) = await SetupAsync();
        var (_, _, boardB, _) = await SetupAsync();

        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = projectA.Id, boardId = boardB.Id, listId = boardB.Lists[0].Id,
            title = "Sneaky", description = (string?)null, type = 0, priority = 2
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
