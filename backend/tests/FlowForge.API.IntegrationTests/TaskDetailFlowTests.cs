using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class TaskDetailFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public TaskDetailFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HttpClient Client, BoardDto Board, TaskCardDto Task, Guid ProjectId)> SetupTaskAsync()
    {
        var tag = Tag();
        var client = _factory.CreateClient();
        var auth = await (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            FirstName: "Detail", LastName: "Tester", Email: $"detail-{tag}@integration.test",
            Password: "Passw0rd123", TenantName: $"Detail Co {tag}", TenantSlug: $"detail-{tag}")))
            .Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var project = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Detail Project",
            key = "DET" + tag[..4].ToUpperInvariant(),
            description = (string?)null,
            color = (string?)null,
            visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;

        var board = (await (await client.GetAsync($"/api/v1/projects/{project.Id}/boards"))
            .Content.ReadFromJsonAsync<List<BoardDto>>())![0];

        var task = (await (await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project.Id,
            boardId = board.Id,
            listId = board.Lists.Single(l => l.Name == "To Do").Id,
            title = "Original title",
            description = (string?)null,
            type = (int)TaskType.Task,
            priority = (int)TaskPriority.Medium
        })).Content.ReadFromJsonAsync<TaskCardDto>())!;

        return (client, board, task, project.Id);
    }

    private static async Task<TaskDetailDto> GetDetail(HttpClient client, Guid taskId) =>
        (await (await client.GetAsync($"/api/v1/tasks/{taskId}")).Content.ReadFromJsonAsync<TaskDetailDto>())!;

    [Fact]
    public async Task EditingATask_PersistsEveryField()
    {
        var (client, board, task, _) = await SetupTaskAsync();
        var due = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", new
        {
            title = "Renamed task",
            description = "Now with a description",
            type = (int)TaskType.Bug,
            priority = (int)TaskPriority.High,
            dueDate = due,
            estimatedHours = 4.5,
            storyPoints = 3,
            boardId = board.Id
        });
        response.EnsureSuccessStatusCode();

        var detail = await GetDetail(client, task.Id);
        detail.Title.Should().Be("Renamed task");
        detail.Description.Should().Be("Now with a description");
        detail.Type.Should().Be("Bug");
        detail.Priority.Should().Be("High");
        detail.DueDate.Should().Be(due);
        detail.EstimatedHours.Should().Be(4.5);
        detail.StoryPoints.Should().Be(3);
    }

    [Fact]
    public async Task LabelsAndWatchers_CanBeAddedAndRemoved_AndShowOnTheBoardCard()
    {
        // Labels and watchers are child rows with composite keys added through the task
        // aggregate, which is exactly where EF Core change tracking can silently go wrong -
        // so this runs against a real database rather than mocks.
        var (client, board, task, projectId) = await SetupTaskAsync();

        var label = (await (await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels",
            new { name = "Backend", color = "#0EA97C" })).Content.ReadFromJsonAsync<LabelDto>())!;

        (await client.PutAsync($"/api/v1/tasks/{task.Id}/labels/{label.Id}?boardId={board.Id}", null)).EnsureSuccessStatusCode();
        (await client.PutAsync($"/api/v1/tasks/{task.Id}/watch", null)).EnsureSuccessStatusCode();

        var detail = await GetDetail(client, task.Id);
        detail.Labels.Should().ContainSingle(l => l.Id == label.Id && l.Name == "Backend");
        detail.IsWatching.Should().BeTrue();

        var boards = await (await client.GetAsync($"/api/v1/projects/{projectId}/boards"))
            .Content.ReadFromJsonAsync<List<BoardDto>>();
        var card = boards![0].Lists.SelectMany(l => l.Tasks).Single(t => t.Id == task.Id);
        card.Labels.Should().ContainSingle(l => l.Id == label.Id);

        (await client.DeleteAsync($"/api/v1/tasks/{task.Id}/labels/{label.Id}?boardId={board.Id}")).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/v1/tasks/{task.Id}/watch")).EnsureSuccessStatusCode();

        detail = await GetDetail(client, task.Id);
        detail.Labels.Should().BeEmpty();
        detail.IsWatching.Should().BeFalse();
    }

    [Fact]
    public async Task CreatingADuplicateLabelName_IsRejected()
    {
        var (client, _, _, projectId) = await SetupTaskAsync();

        (await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new { name = "UI", color = "#6449E0" }))
            .EnsureSuccessStatusCode();
        var duplicate = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new { name = "ui", color = "#6449E0" });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Comments_CanBeAddedEditedAndDeleted_AndKeepTheCountInSync()
    {
        var (client, board, task, _) = await SetupTaskAsync();

        var comment = (await (await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/comments?boardId={board.Id}",
            new { content = "First pass looks good", mentionedUserIds = (List<Guid>?)null }))
            .Content.ReadFromJsonAsync<TaskCommentDto>())!;

        var detail = await GetDetail(client, task.Id);
        detail.Comments.Should().ContainSingle(c => c.Content == "First pass looks good");
        detail.IsWatching.Should().BeTrue("commenting on a task subscribes you to it");

        (await client.PutAsJsonAsync($"/api/v1/comments/{comment.Id}", new { content = "Edited" })).EnsureSuccessStatusCode();
        detail = await GetDetail(client, task.Id);
        detail.Comments.Single().Content.Should().Be("Edited");
        detail.Comments.Single().IsEdited.Should().BeTrue();

        (await client.DeleteAsync($"/api/v1/comments/{comment.Id}")).EnsureSuccessStatusCode();
        detail = await GetDetail(client, task.Id);
        detail.Comments.Should().BeEmpty();

        var card = (await (await client.GetAsync($"/api/v1/boards/{board.Id}")).Content.ReadFromJsonAsync<BoardDto>())!
            .Lists.SelectMany(l => l.Tasks).Single(t => t.Id == task.Id);
        card.CommentCount.Should().Be(0);
    }

    [Fact]
    public async Task Subtasks_ShowUnderTheirParent_AndDeletingTheParentRemovesBoth()
    {
        var (client, board, task, projectId) = await SetupTaskAsync();

        var subtask = (await (await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/subtasks?boardId={board.Id}",
            new { title = "Write the migration" })).Content.ReadFromJsonAsync<SubtaskDto>())!;

        var detail = await GetDetail(client, task.Id);
        detail.Subtasks.Should().ContainSingle(s => s.Id == subtask.Id);

        var nested = await client.PostAsJsonAsync($"/api/v1/tasks/{subtask.Id}/subtasks", new { title = "Too deep" });
        nested.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.DeleteAsync($"/api/v1/tasks/{task.Id}?boardId={board.Id}")).EnsureSuccessStatusCode();

        var cards = (await (await client.GetAsync($"/api/v1/projects/{projectId}/boards"))
            .Content.ReadFromJsonAsync<List<BoardDto>>())![0].Lists.SelectMany(l => l.Tasks).ToList();
        cards.Should().NotContain(c => c.Id == task.Id || c.Id == subtask.Id);

        (await client.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
