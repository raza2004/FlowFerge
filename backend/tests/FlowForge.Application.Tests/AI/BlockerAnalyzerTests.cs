using FlowForge.Application.AI;
using FluentAssertions;

namespace FlowForge.Application.Tests.AI;

public class BlockerAnalyzerTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 10, 30, 0, DateTimeKind.Utc);
    private static readonly Guid Alice = Guid.NewGuid();

    private static BlockerTaskInfo Task(
        string number = "T-1", bool done = false, int? dueInDays = null, Guid? assignee = null,
        string? assigneeName = null, int updatedDaysAgo = 0) =>
        new(Guid.NewGuid(), number, $"Task {number}", done,
            dueInDays.HasValue ? Now.Date.AddDays(dueInDays.Value) : null,
            assignee, assigneeName, Now.AddDays(-updatedDaysAgo));

    [Fact]
    public void HealthyProject_HasNoSignals()
    {
        var tasks = new[]
        {
            Task("T-1", dueInDays: 10, assignee: Alice, assigneeName: "Alice"),
            Task("T-2", done: true, dueInDays: -30),
            Task("T-3", updatedDaysAgo: 40)
        };

        BlockerAnalyzer.Analyze(tasks, Now).Should().BeEmpty("done work and unowned, undated tasks aren't blockers");
    }

    [Theory]
    [InlineData(-1, "Medium", "Overdue by 1 day")]
    [InlineData(-6, "Medium", "Overdue by 6 days")]
    [InlineData(-7, "High", "Overdue by 7 days")]
    public void OverdueTasks_AreFlagged_WithSeverityByDaysLate(int dueInDays, string severity, string detailStart)
    {
        var signals = BlockerAnalyzer.Analyze(new[] { Task(dueInDays: dueInDays) }, Now);

        var signal = signals.Should().ContainSingle().Subject;
        signal.Kind.Should().Be(BlockerAnalyzer.Overdue);
        signal.Severity.Should().Be(severity);
        signal.Detail.Should().StartWith(detailStart);
    }

    [Fact]
    public void TaskDueToday_IsNotOverdueYet()
    {
        BlockerAnalyzer.Analyze(new[] { Task(dueInDays: 0, assignee: Alice, assigneeName: "Alice") }, Now)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "High")]
    [InlineData(1, "High")]
    [InlineData(2, "Medium")]
    [InlineData(3, "Medium")]
    public void UnassignedWorkDueSoon_IsFlagged(int dueInDays, string severity)
    {
        var signal = BlockerAnalyzer.Analyze(new[] { Task(dueInDays: dueInDays) }, Now).Should().ContainSingle().Subject;

        signal.Kind.Should().Be(BlockerAnalyzer.DueSoonUnassigned);
        signal.Severity.Should().Be(severity);
    }

    [Fact]
    public void UnassignedWork_DueLater_OrAssignedWorkDueSoon_IsFine()
    {
        var tasks = new[]
        {
            Task("T-1", dueInDays: 4),
            Task("T-2", dueInDays: 1, assignee: Alice, assigneeName: "Alice")
        };

        BlockerAnalyzer.Analyze(tasks, Now).Should().BeEmpty();
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(14, true)]
    public void OwnedWork_WithNoRecentActivity_IsStale(int idleDays, bool flagged)
    {
        var signals = BlockerAnalyzer.Analyze(
            new[] { Task(assignee: Alice, assigneeName: "Alice", updatedDaysAgo: idleDays) }, Now);

        if (!flagged) { signals.Should().BeEmpty(); return; }
        signals.Should().ContainSingle(s => s.Kind == BlockerAnalyzer.Stale);
        signals[0].Severity.Should().Be(idleDays >= 14 ? "Medium" : "Low");
        signals[0].Detail.Should().Contain("Alice");
    }

    [Fact]
    public void UnownedIdleWork_IsNotStale()
    {
        BlockerAnalyzer.Analyze(new[] { Task(updatedDaysAgo: 60) }, Now).Should().BeEmpty();
    }

    [Fact]
    public void ATaskIsReportedOnce_OverdueWinsOverStale()
    {
        var overdueAndIdle = Task(dueInDays: -3, assignee: Alice, assigneeName: "Alice", updatedDaysAgo: 20);

        var signals = BlockerAnalyzer.Analyze(new[] { overdueAndIdle }, Now);

        signals.Should().ContainSingle().Which.Kind.Should().Be(BlockerAnalyzer.Overdue);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    public void PeopleWithTooMuchOpenWork_AreFlagged(int openTasks, bool flagged)
    {
        var tasks = Enumerable.Range(1, openTasks).Select(i => Task($"T-{i}", assignee: Alice, assigneeName: "Alice")).ToList();

        var signals = BlockerAnalyzer.Analyze(tasks, Now);

        if (!flagged) { signals.Should().BeEmpty(); return; }
        var signal = signals.Should().ContainSingle().Subject;
        signal.Kind.Should().Be(BlockerAnalyzer.Overloaded);
        signal.TaskId.Should().BeNull();
        signal.Detail.Should().Contain("8 open tasks");
    }

    [Fact]
    public void CompletedTasks_DoNotCountTowardsWorkload()
    {
        var tasks = Enumerable.Range(1, 20).Select(i => Task($"T-{i}", done: true, assignee: Alice, assigneeName: "Alice")).ToList();

        BlockerAnalyzer.Analyze(tasks, Now).Should().BeEmpty();
    }

    [Fact]
    public void Signals_AreSortedMostSevereFirst()
    {
        var tasks = new[]
        {
            Task("T-1", assignee: Alice, assigneeName: "Alice", updatedDaysAgo: 8),   // Low
            Task("T-2", dueInDays: -10),                                              // High
            Task("T-3", dueInDays: -2)                                                // Medium
        };

        BlockerAnalyzer.Analyze(tasks, Now).Select(s => s.Severity).Should().Equal("High", "Medium", "Low");
    }
}
