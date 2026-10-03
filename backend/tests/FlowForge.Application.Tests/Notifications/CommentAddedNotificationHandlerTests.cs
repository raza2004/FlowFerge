using FlowForge.Application.Notifications.EventHandlers;
using FlowForge.Application.Notifications.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Enums;
using FlowForge.Domain.Notifications.Repositories;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Events;
using FlowForge.Domain.Projects.Repositories;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.Notifications;

public class CommentAddedNotificationHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly List<Notification> _saved = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly ProjectTask _task;

    public CommentAddedNotificationHandlerTests()
    {
        _task = ProjectTask.Create(_tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PROJ-7", "Fix login", TaskType.Bug, TaskPriority.High, Guid.NewGuid()).Value;

        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.GetByIdWithDetailsAsync(_task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_task);
        _uow.SetupGet(x => x.Tasks).Returns(tasks.Object);
        _uow.SetupGet(x => x.Users).Returns(Mock.Of<IUserRepository>());

        var notifications = new Mock<INotificationRepository>();
        notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => _saved.Add(n))
            .Returns(Task.CompletedTask);
        _uow.SetupGet(x => x.Notifications).Returns(notifications.Object);
    }

    private Task Handle(params Guid[] mentioned) =>
        new CommentAddedNotificationHandler(_uow.Object, _dispatcher.Object).Handle(
            new CommentAddedEvent(Guid.NewGuid(), _task.Id, _authorId, "Looks good to me", mentioned.ToList(), _tenantId),
            CancellationToken.None);

    [Fact]
    public async Task MentionedUser_GetsAMentionNotification_NotADuplicateWatcherOne()
    {
        var mentionedWatcher = Guid.NewGuid();
        _task.AddWatcher(mentionedWatcher);

        await Handle(mentionedWatcher);

        _saved.Should().ContainSingle();
        _saved[0].UserId.Should().Be(mentionedWatcher);
        _saved[0].Type.Should().Be(NotificationType.Mentioned);
    }

    [Fact]
    public async Task WatchersAndAssignee_GetCommentNotifications_ButNeverTheAuthor()
    {
        var watcher = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        _task.AddWatcher(watcher);
        _task.AddWatcher(_authorId);
        _task.Assign(assignee, Guid.NewGuid(), _tenantId);

        await Handle();

        _saved.Select(n => n.UserId).Should().BeEquivalentTo(new[] { watcher, assignee });
        _saved.Should().OnlyContain(n => n.Type == NotificationType.CommentAdded);
        _dispatcher.Verify(x => x.DispatchAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NoOneToNotify_SavesNothing()
    {
        _task.AddWatcher(_authorId);

        await Handle();

        _saved.Should().BeEmpty();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
