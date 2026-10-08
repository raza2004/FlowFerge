using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FluentAssertions;

namespace FlowForge.Domain.Tests.Projects;

public class SprintTests
{
    private static readonly DateTime Start = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    private static Sprint NewSprint(int days = 14) =>
        Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", Start, Start.AddDays(days), Guid.NewGuid(), "Ship it").Value;

    [Fact]
    public void Create_RejectsBadDatesAndNames()
    {
        var tenant = Guid.NewGuid();
        Sprint.Create(tenant, Guid.NewGuid(), "x", Start, Start, Guid.NewGuid()).IsFailure.Should().BeTrue();
        Sprint.Create(tenant, Guid.NewGuid(), "x", Start, Start.AddDays(61), Guid.NewGuid()).IsFailure.Should().BeTrue();
        Sprint.Create(tenant, Guid.NewGuid(), " ", Start, Start.AddDays(7), Guid.NewGuid()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_PlanningToActiveToCompleted_AndNoGoingBack()
    {
        var sprint = NewSprint();
        sprint.Status.Should().Be(SprintStatus.Planning);

        sprint.Start().IsSuccess.Should().BeTrue();
        sprint.Start().IsFailure.Should().BeTrue("a started sprint can't be started again");

        sprint.Complete("Went well").IsSuccess.Should().BeTrue();
        sprint.Status.Should().Be(SprintStatus.Completed);
        sprint.RetrospectiveNotes.Should().Be("Went well");

        sprint.Complete().IsFailure.Should().BeTrue();
        sprint.Cancel().IsFailure.Should().BeTrue("finished sprints can't be cancelled");
        sprint.UpdateDetails("Renamed", null, Start, Start.AddDays(7)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PlannedSprint_CannotBeCompleted()
    {
        NewSprint().Complete().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancel_WorksFromPlanningAndActive()
    {
        NewSprint().Cancel().IsSuccess.Should().BeTrue();

        var active = NewSprint();
        active.Start();
        active.Cancel().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UpdateDetails_ValidatesLikeCreate()
    {
        var sprint = NewSprint();

        sprint.UpdateDetails("", null, Start, Start.AddDays(7)).IsFailure.Should().BeTrue();
        sprint.UpdateDetails("Ok", null, Start, Start.AddDays(-1)).IsFailure.Should().BeTrue();
        sprint.UpdateDetails("  Renamed  ", "  goal ", Start, Start.AddDays(10)).IsSuccess.Should().BeTrue();

        sprint.Name.Should().Be("Renamed");
        sprint.Goal.Should().Be("goal");
    }

    [Fact]
    public void Retrospective_CanOnlyBeEditedOnceCompleted()
    {
        var sprint = NewSprint();
        sprint.UpdateRetrospective("too early").IsFailure.Should().BeTrue();

        sprint.Start();
        sprint.Complete();
        sprint.UpdateRetrospective("  Better estimates next time  ").IsSuccess.Should().BeTrue();
        sprint.RetrospectiveNotes.Should().Be("Better estimates next time");

        sprint.UpdateRetrospective("  ").IsSuccess.Should().BeTrue();
        sprint.RetrospectiveNotes.Should().BeNull();
    }
}
