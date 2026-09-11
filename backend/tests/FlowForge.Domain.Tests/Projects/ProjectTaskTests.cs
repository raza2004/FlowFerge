using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Projects;

public class ProjectTaskTests
{
    private static ProjectTask CreateTask() =>
        ProjectTask.Create(
            tenantId: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            boardId: Guid.NewGuid(),
            listId: Guid.NewGuid(),
            taskNumber: "PROJ-1",
            title: "Do the thing",
            type: TaskType.Task,
            priority: TaskPriority.Medium,
            createdById: Guid.NewGuid()
        ).Value;

    [Fact]
    public void Create_SetsInitialStatusToTodoAndNotCompleted()
    {
        var task = CreateTask();

        task.Status.Should().Be("Todo");
        task.IsCompleted.Should().BeFalse();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithBlankTitle_Fails()
    {
        var result = ProjectTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PROJ-1", "   ", TaskType.Task, TaskPriority.Medium, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MoveTo_ToADifferentList_RaisesTaskMovedEvent()
    {
        var task = CreateTask();
        var newListId = Guid.NewGuid();

        task.MoveTo(newListId, 0, Guid.NewGuid(), Guid.NewGuid());

        task.ListId.Should().Be(newListId);
        task.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "TaskMovedEvent");
    }

    [Fact]
    public void MoveTo_SamePositionAndList_IsANoOpAndRaisesNoMoveEvent()
    {
        var task = CreateTask();
        var sameList = task.ListId;

        task.MoveTo(sameList, task.Position, Guid.NewGuid(), Guid.NewGuid());

        task.DomainEvents.Should().NotContain(e => e.GetType().Name == "TaskMovedEvent");
    }

    [Fact]
    public void ChangeStatus_ToADoneState_SetsCompletedAt()
    {
        // Regression test: MoveTaskCommandHandler used to move a task between lists
        // without ever touching Status/CompletedAt, so dragging a card to a "Done"
        // column never actually marked the task complete anywhere (dashboard stats,
        // "My Work", etc. all stayed wrong). ChangeStatus is now the single place that
        // enforces the invariant "isDoneState says whether CompletedAt is set" - driven
        // by the destination list's IsDoneColumn flag, not by string-matching the name.
        var task = CreateTask();

        task.ChangeStatus("Done", isDoneState: true, Guid.NewGuid(), Guid.NewGuid());

        task.Status.Should().Be("Done");
        task.IsCompleted.Should().BeTrue();
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangeStatus_BackToANonDoneState_ClearsCompletedAt()
    {
        var task = CreateTask();
        task.ChangeStatus("Done", isDoneState: true, Guid.NewGuid(), Guid.NewGuid());

        task.ChangeStatus("In Progress", isDoneState: false, Guid.NewGuid(), Guid.NewGuid());

        task.IsCompleted.Should().BeFalse();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeStatus_MovingBetweenTwoDoneLists_KeepsOriginalCompletedAt()
    {
        // A task moved from one "done" list to another (e.g. "Done" -> "Shipped") should
        // not reset when it was actually completed.
        var task = CreateTask();
        task.ChangeStatus("Done", isDoneState: true, Guid.NewGuid(), Guid.NewGuid());
        var firstCompletedAt = task.CompletedAt;

        task.ChangeStatus("Shipped", isDoneState: true, Guid.NewGuid(), Guid.NewGuid());

        task.CompletedAt.Should().Be(firstCompletedAt);
    }

    [Fact]
    public void ChangeStatus_ToTheSameStatusName_RaisesNoStatusChangedEvent()
    {
        var task = CreateTask();

        task.ChangeStatus("Todo", isDoneState: false, Guid.NewGuid(), Guid.NewGuid());

        task.DomainEvents.Should().NotContain(e => e.GetType().Name == "TaskStatusChangedEvent");
    }

    [Fact]
    public void ChangeStatus_ToADifferentStatusName_RaisesTaskStatusChangedEvent()
    {
        var task = CreateTask();

        task.ChangeStatus("In Progress", isDoneState: false, Guid.NewGuid(), Guid.NewGuid());

        task.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "TaskStatusChangedEvent");
    }

    [Fact]
    public void ChangeStatus_WithBlankStatus_Fails()
    {
        var task = CreateTask();

        var result = task.ChangeStatus("  ", isDoneState: false, Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Assign_ToAUser_SetsAssigneeAndRaisesEvent()
    {
        var task = CreateTask();
        var assigneeId = Guid.NewGuid();

        var result = task.Assign(assigneeId, Guid.NewGuid(), Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        task.AssigneeId.Should().Be(assigneeId);
        task.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "TaskAssignedEvent");
    }

    [Fact]
    public void Assign_WithEmptyGuid_Fails()
    {
        var task = CreateTask();

        var result = task.Assign(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        task.AssigneeId.Should().BeNull();
    }

    [Fact]
    public void IsOverdue_WhenDueDateIsInThePastAndNotCompleted_IsTrue()
    {
        var task = CreateTask();
        task.SetDueDate(DateTime.UtcNow.AddDays(-1));

        task.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_WhenCompleted_IsFalseEvenIfDueDateWasInThePast()
    {
        var task = CreateTask();
        task.SetDueDate(DateTime.UtcNow.AddDays(-1));

        task.ChangeStatus("Done", isDoneState: true, Guid.NewGuid(), Guid.NewGuid());

        task.IsOverdue.Should().BeFalse();
    }
}
