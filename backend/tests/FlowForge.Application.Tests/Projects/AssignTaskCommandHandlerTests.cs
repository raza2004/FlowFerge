using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.Commands;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Repositories;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.Projects;

public class AssignTaskCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ProjectTask _task;

    public AssignTaskCommandHandlerTests()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);

        _task = ProjectTask.Create(
            _tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PROJ-1", "Fix the bug", TaskType.Bug, TaskPriority.High, Guid.NewGuid()).Value;

        var tasksRepo = new Mock<ITaskRepository>();
        tasksRepo.Setup(x => x.GetByIdAsync(_task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_task);
        _uow.SetupGet(x => x.Tasks).Returns(tasksRepo.Object);
    }

    private AssignTaskCommandHandler CreateHandler() => new(_uow.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_WithAValidAssignee_AssignsTheTask()
    {
        var assigneeId = Guid.NewGuid();

        var result = await CreateHandler().Handle(new AssignTaskCommand(_task.Id, assigneeId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _task.AssigneeId.Should().Be(assigneeId);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithATaskThatDoesNotExist_Fails()
    {
        Mock.Get(_uow.Object.Tasks)
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTask?)null);

        var result = await CreateHandler().Handle(new AssignTaskCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Task.NotFound");
    }

    [Fact]
    public async Task Handle_WithoutAnActiveTenant_Fails()
    {
        _currentUser.SetupGet(x => x.TenantId).Returns((Guid?)null);

        var result = await CreateHandler().Handle(new AssignTaskCommand(_task.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.NoTenant");
    }
}
