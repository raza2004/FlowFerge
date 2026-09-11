using FlowForge.Application.Notifications.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.ValueObjects;
using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Enums;
using FlowForge.Domain.Notifications.Repositories;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Events;
using FlowForge.Domain.Projects.Repositories;
using FlowForge.Domain.Workflows;
using FlowForge.Domain.Workflows.Enums;
using FlowForge.Domain.Workflows.Repositories;
using FlowForge.Application.Workflows.EventHandlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FlowForge.Application.Tests.Workflows;

public class TaskMovedAutomationHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _boardId = Guid.NewGuid();
    private readonly Guid _fromListId = Guid.NewGuid();
    private readonly Guid _toListId = Guid.NewGuid();
    private readonly Guid _movedById = Guid.NewGuid();

    private ProjectTask _task;

    public TaskMovedAutomationHandlerTests()
    {
        _task = ProjectTask.Create(
            _tenantId, _projectId, _boardId, _toListId,
            "PROJ-1", "Ship the release", TaskType.Task, TaskPriority.Medium, Guid.NewGuid()).Value;

        var tasksRepo = new Mock<ITaskRepository>();
        tasksRepo.Setup(x => x.GetByIdAsync(_task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_task);
        _uow.SetupGet(x => x.Tasks).Returns(tasksRepo.Object);

        var project = Project.Create(_tenantId, "Automation Demo", FlowForge.Domain.Projects.ValueObjects.ProjectKey.Create("AUTO").Value, Guid.NewGuid(), Guid.NewGuid()).Value;
        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(x => x.GetByIdAsync(_projectId, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _uow.SetupGet(x => x.Projects).Returns(projectsRepo.Object);

        _uow.SetupGet(x => x.Notifications).Returns(Mock.Of<INotificationRepository>());
        _uow.SetupGet(x => x.AutomationRules).Returns(Mock.Of<IAutomationRuleRepository>());
    }

    private TaskMovedAutomationHandler CreateHandler() => new(_uow.Object, _dispatcher.Object, Mock.Of<ILogger<TaskMovedAutomationHandler>>());

    private void SetupRules(params AutomationRule[] rules)
    {
        Mock.Get(_uow.Object.AutomationRules)
            .Setup(x => x.GetEnabledByTriggerAsync(_projectId, _toListId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);
    }

    private AutomationRule CreateRule(AutomationActionType actionType, Guid actionUserId) =>
        AutomationRule.Create(
            _tenantId, _projectId, "Test rule", AutomationTriggerType.TaskMovedToList, _toListId,
            actionType, actionUserId, Guid.NewGuid()).Value;

    [Fact]
    public async Task Handle_WithNoMatchingRules_DoesNothing()
    {
        SetupRules();

        await CreateHandler().Handle(new TaskMovedEvent(_task.Id, _fromListId, _toListId, _movedById, _tenantId), CancellationToken.None);

        _dispatcher.Verify(x => x.DispatchAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithANotifyRule_CreatesAndDispatchesANotificationWithoutAssigning()
    {
        var recipientId = Guid.NewGuid();
        SetupRules(CreateRule(AutomationActionType.NotifyUser, recipientId));

        Notification? added = null;
        Mock.Get(_uow.Object.Notifications)
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => added = n)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new TaskMovedEvent(_task.Id, _fromListId, _toListId, _movedById, _tenantId), CancellationToken.None);

        added.Should().NotBeNull();
        added!.UserId.Should().Be(recipientId);
        added.Type.Should().Be(NotificationType.AutomationTriggered);
        _task.AssigneeId.Should().BeNull();
        _dispatcher.Verify(x => x.DispatchAsync(added, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnAssignRule_AssignsTheTaskAndDispatchesATaskAssignedNotification()
    {
        var assigneeId = Guid.NewGuid();
        SetupRules(CreateRule(AutomationActionType.AssignUser, assigneeId));

        Notification? added = null;
        Mock.Get(_uow.Object.Notifications)
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => added = n)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new TaskMovedEvent(_task.Id, _fromListId, _toListId, _movedById, _tenantId), CancellationToken.None);

        _task.AssigneeId.Should().Be(assigneeId);
        added!.Type.Should().Be(NotificationType.TaskAssigned);
        added.UserId.Should().Be(assigneeId);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DispatchesOnlyAfterSaveChanges()
    {
        // The whole point of collecting notifications into toDispatch and dispatching
        // after SaveChangesAsync is so a client is never told about something that
        // turned out not to persist. Verify the ordering, not just that both happened.
        var callOrder = new List<string>();
        SetupRules(CreateRule(AutomationActionType.NotifyUser, Guid.NewGuid()));

        Mock.Get(_uow.Object.Notifications)
            .Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .ReturnsAsync(1);
        _dispatcher.Setup(x => x.DispatchAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("dispatch"))
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new TaskMovedEvent(_task.Id, _fromListId, _toListId, _movedById, _tenantId), CancellationToken.None);

        callOrder.Should().Equal("save", "dispatch");
    }

    [Fact]
    public async Task Handle_WhenTheTaskNoLongerExists_DoesNothing()
    {
        var missingTaskId = Guid.NewGuid();

        await CreateHandler().Handle(new TaskMovedEvent(missingTaskId, _fromListId, _toListId, _movedById, _tenantId), CancellationToken.None);

        Mock.Get(_uow.Object.AutomationRules).Verify(
            x => x.GetEnabledByTriggerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
