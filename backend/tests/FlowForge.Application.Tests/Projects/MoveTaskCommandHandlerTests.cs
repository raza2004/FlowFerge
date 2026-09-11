using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.Commands;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Repositories;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.Projects;

public class MoveTaskCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private ProjectTask _task = null!;
    private Board _board = null!;
    private BoardList _todoList = null!;
    private BoardList _doneList = null!;

    public MoveTaskCommandHandlerTests()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        var projectId = Guid.NewGuid();
        _board = Board.Create(projectId, _tenantId, "Main Board", BoardType.Kanban, _userId).Value;
        _todoList = BoardList.Create(_board.Id, _tenantId, "To Do").Value;
        _doneList = BoardList.Create(_board.Id, _tenantId, "Done").Value;
        _doneList.UpdateDetails("Done", "#10b981", null, isDoneColumn: true);
        _board.AddList(_todoList);
        _board.AddList(_doneList);

        _task = ProjectTask.Create(
            _tenantId, projectId, _board.Id, _todoList.Id,
            "PROJ-1", "Ship it", TaskType.Task, TaskPriority.Medium, _userId).Value;

        var tasksRepo = new Mock<ITaskRepository>();
        tasksRepo.Setup(x => x.GetByIdAsync(_task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_task);
        _uow.SetupGet(x => x.Tasks).Returns(tasksRepo.Object);

        var boardsRepo = new Mock<IBoardRepository>();
        boardsRepo.Setup(x => x.GetByIdWithListsAsync(_board.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_board);
        _uow.SetupGet(x => x.Boards).Returns(boardsRepo.Object);
    }

    private MoveTaskCommandHandler CreateHandler() => new(_uow.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_MovingToADoneList_MarksTheTaskCompleted()
    {
        // Regression test: MoveTaskCommandHandler used to only update ListId/Position,
        // never Status/CompletedAt - so dragging a card to "Done" never actually
        // completed it anywhere the rest of the app reads that from (dashboard stats,
        // "My Work", project summary counts).
        var handler = CreateHandler();

        var result = await handler.Handle(new MoveTaskCommand(_task.Id, _doneList.Id, 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _task.Status.Should().Be("Done");
        _task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MovingToANonDoneList_KeepsTheTaskIncomplete()
    {
        var inProgress = BoardList.Create(_board.Id, _tenantId, "In Progress").Value;
        _board.AddList(inProgress);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveTaskCommand(_task.Id, inProgress.Id, 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _task.Status.Should().Be("In Progress");
        _task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithATaskFromAnotherTenant_Fails()
    {
        var otherTenantTask = ProjectTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), _board.Id, _todoList.Id,
            "OTHER-1", "Not yours", TaskType.Task, TaskPriority.Medium, Guid.NewGuid()).Value;

        Mock.Get(_uow.Object.Tasks)
            .Setup(x => x.GetByIdAsync(otherTenantTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherTenantTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveTaskCommand(otherTenantTask.Id, _doneList.Id, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Task.NotFound");
    }

    [Fact]
    public async Task Handle_WithAListThatDoesNotExistOnTheBoard_Fails()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new MoveTaskCommand(_task.Id, Guid.NewGuid(), 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BoardList.NotFound");
    }
}
