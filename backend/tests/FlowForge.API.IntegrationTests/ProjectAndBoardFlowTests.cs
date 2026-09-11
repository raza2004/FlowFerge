using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.API.IntegrationTests;

[Collection("Integration")]
public class ProjectAndBoardFlowTests
{
    private readonly CustomWebApplicationFactory _factory;
    public ProjectAndBoardFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid UserId)> AuthenticatedClientAsync(string tag)
    {
        var client = _factory.CreateClient();
        var req = new RegisterRequest(
            FirstName: "Test", LastName: "User",
            Email: $"proj-{tag}@integration.test", Password: "Passw0rd123",
            TenantName: $"Project Co {tag}", TenantSlug: $"project-{tag}");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", req);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth.User.Id);
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task CreateProject_AutoCreatesABoardWithADoneListMarkedAsDone()
    {
        // Regression coverage for the same bug the unit tests exercise via mocks, but
        // proven here end to end through the real API, real MediatR pipeline, and a
        // real database - the closest thing to what a browser dragging a card actually
        // does.
        var (client, _) = await AuthenticatedClientAsync(Tag());

        var createResponse = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Payments",
            key = "PAY" + Tag()[..4].ToUpperInvariant(),
            description = (string?)null,
            color = (string?)null,
            visibility = (int)ProjectVisibility.TenantWide
        });
        var project = await createResponse.Content.ReadFromJsonAsync<ProjectDto>();

        var boardsResponse = await client.GetAsync($"/api/v1/projects/{project!.Id}/boards");
        var boards = await boardsResponse.Content.ReadFromJsonAsync<List<BoardDto>>();

        boards.Should().ContainSingle();
        var doneList = boards![0].Lists.Single(l => l.Name == "Done");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowForgeDbContext>();
        var doneListEntity = await db.BoardLists.SingleAsync(l => l.Id == doneList.Id);

        doneListEntity.IsDoneColumn.Should().BeTrue();
    }

    [Fact]
    public async Task MovingATaskToTheDoneList_MarksItCompletedInMyTasks()
    {
        var (client, userId) = await AuthenticatedClientAsync(Tag());

        var project = (await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Release Plan",
            key = "REL" + Tag()[..4].ToUpperInvariant(),
            description = (string?)null,
            color = (string?)null,
            visibility = (int)ProjectVisibility.TenantWide
        })).Content.ReadFromJsonAsync<ProjectDto>())!;

        var board = (await (await client.GetAsync($"/api/v1/projects/{project.Id}/boards"))
            .Content.ReadFromJsonAsync<List<BoardDto>>())![0];
        var todoList = board.Lists.Single(l => l.Name == "To Do");
        var doneList = board.Lists.Single(l => l.Name == "Done");

        var task = (await (await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project.Id,
            boardId = board.Id,
            listId = todoList.Id,
            title = "Ship the release",
            description = (string?)null,
            type = (int)TaskType.Task,
            priority = (int)TaskPriority.Medium
        })).Content.ReadFromJsonAsync<TaskCardDto>())!;

        var assignResponse = await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/assign", new
        {
            assigneeId = userId,
            boardId = board.Id
        });
        assignResponse.EnsureSuccessStatusCode();

        var moveResponse = await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new
        {
            newListId = doneList.Id,
            newPosition = 0,
            boardId = board.Id
        });
        moveResponse.EnsureSuccessStatusCode();

        var myTasks = await (await client.GetAsync("/api/v1/dashboard/my-tasks"))
            .Content.ReadFromJsonAsync<List<MyTaskDto>>();

        var moved = myTasks!.Single(t => t.Id == task.Id);
        moved.Status.Should().Be("Done");
        moved.IsOverdue.Should().BeFalse();
    }
}
