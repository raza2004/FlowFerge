using FlowForge.Domain.Notifications;
using FlowForge.Domain.Notifications.Enums;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Notifications;

public class NotificationTests
{
    [Fact]
    public void Create_WithValidData_StartsUnread()
    {
        var result = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.AutomationTriggered,
            "Automation triggered", "\"Ship it\" triggered automation \"Notify on Done\".");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsRead.Should().BeFalse();
        result.Value.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithBlankTitle_Fails()
    {
        var result = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.TaskAssigned, "  ", "message");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkRead_SetsIsReadAndReadAt()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message").Value;

        notification.MarkRead();

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkRead_CalledTwice_KeepsTheOriginalReadAt()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message").Value;

        notification.MarkRead();
        var firstReadAt = notification.ReadAt;

        notification.MarkRead();

        notification.ReadAt.Should().Be(firstReadAt);
    }
}
