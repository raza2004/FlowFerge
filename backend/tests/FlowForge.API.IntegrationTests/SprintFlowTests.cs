using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class SprintFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public SprintFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];
    private static DateTime Today => DateTime.UtcNow.Date;

    private async Task<(HttpClient Client, ProjectDto Project, BoardDto Board)> SetupAsync()
    {
        var tag = Tag();
        var client = _factory.CreateClient();
        var auth = (await (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            "Sprint", "Planner", $"sprint-{tag}@integration.test", "Passw0rd123", $"Sprints {tag}", $"sprints-{tag}")))
            .Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var project = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Sprints", key = "SPR" + tag[..4].ToUpperInvariant(),
            description = (string?)null, color = (string?)null, visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var board = (await (await client.GetAsync($"/api/v1/projects/{project.Id}/boards")).Content.ReadFromJsonAsync<List<BoardDto>>())![0];
        return (client, project, board);
    }

    private static async Task<SprintDto> CreateSprint(HttpClient client, Guid projectId, string name, int startOffsetDays = 0, int lengthDays = 7)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/sprints",
            new { name, goal = "Goal", startDate = Today.AddDays(startOffsetDays), endDate = Today.AddDays(startOffsetDays + lengthDays) });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SprintDto>())!;
    }

    private static async Task<TaskCardDto> CreateTask(HttpClient client, ProjectDto project, BoardDto board, string title, int? points = null)
    {
        var task = (await (await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project.Id, boardId = board.Id, listId = board.Lists.Single(l => l.Name == "To Do").Id,
            title, description = (string?)null, type = 0, priority = 2
        })).Content.ReadFromJsonAsync<TaskCardDto>())!;

        if (points.HasValue)
            (await client.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", new
            {
                title, description = (string?)null, type = 0, priority = 2,
                dueDate = (DateTime?)null, estimatedHours = (double?)null, storyPoints = points, boardId = board.Id
            })).EnsureSuccessStatusCode();
        return task;
    }

    private static async Task MoveToDone(HttpClient client, BoardDto board, TaskCardDto task) =>
        (await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new
        {
            newListId = board.Lists.Single(l => l.Name == "Done").Id, newPosition = 0, boardId = board.Id
        })).EnsureSuccessStatusCode();

    private static Task<HttpResponseMessage> AssignToSprint(HttpClient client, Guid taskId, Guid? sprintId) =>
        client.PutAsJsonAsync($"/api/v1/tasks/{taskId}/sprint", new { sprintId });

    private static async Task<SprintDetailDto> Detail(HttpClient client, Guid sprintId) =>
        (await (await client.GetAsync($"/api/v1/sprints/{sprintId}")).Content.ReadFromJsonAsync<SprintDetailDto>())!;

    [Fact]
    public async Task Backlog_ShowsOnlyUnscheduledOpenTopLevelWork()
    {
        var (client, project, board) = await SetupAsync();
        var sprint = await CreateSprint(client, project.Id, "Sprint 1");
        var inBacklog = await CreateTask(client, project, board, "Backlog item");
        var scheduled = await CreateTask(client, project, board, "Scheduled item");
        var finished = await CreateTask(client, project, board, "Finished item");
        await MoveToDone(client, board, finished);
        (await client.PostAsJsonAsync($"/api/v1/tasks/{inBacklog.Id}/subtasks?boardId={board.Id}", new { title = "A subtask" })).EnsureSuccessStatusCode();
        (await AssignToSprint(client, scheduled.Id, sprint.Id)).EnsureSuccessStatusCode();

        var backlog = await (await client.GetAsync($"/api/v1/projects/{project.Id}/backlog")).Content.ReadFromJsonAsync<List<SprintTaskDto>>();

        backlog!.Select(t => t.Title).Should().Equal("Backlog item");
    }

    [Fact]
    public async Task OnlyOneSprintCanBeActiveAtATime()
    {
        var (client, project, _) = await SetupAsync();
        var first = await CreateSprint(client, project.Id, "Sprint 1");
        var second = await CreateSprint(client, project.Id, "Sprint 2", startOffsetDays: 8);

        (await client.PostAsync($"/api/v1/sprints/{first.Id}/start", null)).EnsureSuccessStatusCode();
        var secondStart = await client.PostAsync($"/api/v1/sprints/{second.Id}/start", null);

        secondStart.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Burndown_ReflectsCompletedWork_AndSprintStatsAddUp()
    {
        var (client, project, board) = await SetupAsync();
        var sprint = await CreateSprint(client, project.Id, "Sprint 1", startOffsetDays: -2, lengthDays: 6);
        var big = await CreateTask(client, project, board, "Big", points: 5);
        var small = await CreateTask(client, project, board, "Small", points: 3);
        await AssignToSprint(client, big.Id, sprint.Id);
        await AssignToSprint(client, small.Id, sprint.Id);
        (await client.PostAsync($"/api/v1/sprints/{sprint.Id}/start", null)).EnsureSuccessStatusCode();

        await MoveToDone(client, board, big);

        var detail = await Detail(client, sprint.Id);
        detail.Sprint.Status.Should().Be("Active");
        detail.Sprint.TotalPoints.Should().Be(8);
        detail.Sprint.DonePoints.Should().Be(5);
        detail.Sprint.TaskCount.Should().Be(2);
        detail.Sprint.DoneCount.Should().Be(1);

        detail.Burndown.Unit.Should().Be("points");
        detail.Burndown.Total.Should().Be(8);
        var today = detail.Burndown.Points.Single(p => p.Date.Date == Today);
        today.Remaining.Should().Be(3, "5 of 8 points were finished today");
        detail.Burndown.Points.Where(p => p.Date.Date > Today).Should().OnlyContain(p => p.Remaining == null);
    }

    [Fact]
    public async Task CompletingASprint_MovesUnfinishedWorkToTheChosenDestination_AndKeepsTheRetro()
    {
        var (client, project, board) = await SetupAsync();
        var current = await CreateSprint(client, project.Id, "Sprint 1");
        var next = await CreateSprint(client, project.Id, "Sprint 2", startOffsetDays: 8);
        var done = await CreateTask(client, project, board, "Done work");
        var carried = await CreateTask(client, project, board, "Carried over");
        await AssignToSprint(client, done.Id, current.Id);
        await AssignToSprint(client, carried.Id, current.Id);
        (await client.PostAsync($"/api/v1/sprints/{current.Id}/start", null)).EnsureSuccessStatusCode();
        await MoveToDone(client, board, done);

        (await client.PostAsJsonAsync($"/api/v1/sprints/{current.Id}/complete",
            new { retrospectiveNotes = "Estimates were optimistic", moveIncompleteToSprintId = next.Id })).EnsureSuccessStatusCode();

        var finished = await Detail(client, current.Id);
        finished.Sprint.Status.Should().Be("Completed");
        finished.Sprint.RetrospectiveNotes.Should().Be("Estimates were optimistic");
        finished.Tasks.Select(t => t.Title).Should().Equal("Done work");

        (await Detail(client, next.Id)).Tasks.Select(t => t.Title).Should().Equal("Carried over");

        (await client.PutAsJsonAsync($"/api/v1/sprints/{current.Id}/retrospective", new { notes = "Revised notes" })).EnsureSuccessStatusCode();
        (await Detail(client, current.Id)).Sprint.RetrospectiveNotes.Should().Be("Revised notes");

        // Completed history can't be reshuffled.
        (await AssignToSprint(client, done.Id, null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CompletingWithoutADestination_SendsUnfinishedWorkBackToTheBacklog()
    {
        var (client, project, board) = await SetupAsync();
        var sprint = await CreateSprint(client, project.Id, "Sprint 1");
        var task = await CreateTask(client, project, board, "Unfinished");
        await AssignToSprint(client, task.Id, sprint.Id);
        (await client.PostAsync($"/api/v1/sprints/{sprint.Id}/start", null)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync($"/api/v1/sprints/{sprint.Id}/complete",
            new { retrospectiveNotes = (string?)null, moveIncompleteToSprintId = (Guid?)null })).EnsureSuccessStatusCode();

        var backlog = await (await client.GetAsync($"/api/v1/projects/{project.Id}/backlog")).Content.ReadFromJsonAsync<List<SprintTaskDto>>();
        backlog!.Should().ContainSingle(t => t.Id == task.Id);
    }

    [Fact]
    public async Task CancellingASprint_ReturnsItsTasksToTheBacklog()
    {
        var (client, project, board) = await SetupAsync();
        var sprint = await CreateSprint(client, project.Id, "Doomed");
        var task = await CreateTask(client, project, board, "Rescued");
        await AssignToSprint(client, task.Id, sprint.Id);

        (await client.PostAsync($"/api/v1/sprints/{sprint.Id}/cancel", null)).EnsureSuccessStatusCode();

        var backlog = await (await client.GetAsync($"/api/v1/projects/{project.Id}/backlog")).Content.ReadFromJsonAsync<List<SprintTaskDto>>();
        backlog!.Should().ContainSingle(t => t.Id == task.Id);
        (await Detail(client, sprint.Id)).Sprint.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task SubtasksCannotBePlannedAlone_AndSprintsStayInsideTheirProject()
    {
        var (client, project, board) = await SetupAsync();
        var sprint = await CreateSprint(client, project.Id, "Sprint 1");
        var parent = await CreateTask(client, project, board, "Parent");
        var subtask = (await (await client.PostAsJsonAsync($"/api/v1/tasks/{parent.Id}/subtasks?boardId={board.Id}",
            new { title = "Child" })).Content.ReadFromJsonAsync<SubtaskDto>())!;

        (await AssignToSprint(client, subtask.Id, sprint.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A second project in the same workspace: its sprint is off limits to this project's tasks.
        var otherProject = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Other", key = "OTH" + Tag()[..4].ToUpperInvariant(),
            description = (string?)null, color = (string?)null, visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var otherSprint = await CreateSprint(client, otherProject.Id, "Other sprint");

        (await AssignToSprint(client, parent.Id, otherSprint.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AssignToSprint(client, parent.Id, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AssignToSprint(client, parent.Id, sprint.Id)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task BoardCards_CarryTheirSprint_SoTheBoardCanFilterToTheActiveSprint()
    {
        var (client, project, board) = await SetupAsync();
        var sprint = await CreateSprint(client, project.Id, "Sprint 1");
        var inSprint = await CreateTask(client, project, board, "In sprint");
        var outside = await CreateTask(client, project, board, "Outside");
        await AssignToSprint(client, inSprint.Id, sprint.Id);

        var cards = (await (await client.GetAsync($"/api/v1/boards/{board.Id}")).Content.ReadFromJsonAsync<BoardDto>())!
            .Lists.SelectMany(l => l.Tasks).ToList();

        cards.Single(c => c.Id == inSprint.Id).SprintId.Should().Be(sprint.Id);
        cards.Single(c => c.Id == outside.Id).SprintId.Should().BeNull();
    }
}
