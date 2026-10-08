using FlowForge.Application.Projects.Sprints;
using FluentAssertions;

namespace FlowForge.Application.Tests.Projects;

public class BurndownCalculatorTests
{
    private static readonly DateTime Start = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc); // 5-day sprint: Mar 2..6
    private static readonly DateTime End = new(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime Day(int n) => Start.AddDays(n).AddHours(15);

    [Fact]
    public void IdealLine_FallsLinearlyFromTotalToZero()
    {
        var tasks = new[] { new BurndownTask(10, null) };

        var result = BurndownCalculator.Calculate(tasks, Start, End, End, sprintFinished: false);

        result.Points.Select(p => p.Ideal).Should().Equal(10, 7.5, 5, 2.5, 0);
    }

    [Fact]
    public void Remaining_DropsOnTheDayEachTaskWasCompleted()
    {
        var tasks = new[]
        {
            new BurndownTask(5, Day(1)),
            new BurndownTask(3, Day(3)),
            new BurndownTask(2, null)
        };

        var result = BurndownCalculator.Calculate(tasks, Start, End, End, sprintFinished: false);

        result.Unit.Should().Be("points");
        result.Total.Should().Be(10);
        result.Points.Select(p => p.Remaining).Should().Equal(10, 5, 5, 2, 2);
    }

    [Fact]
    public void Days_InTheFuture_HaveNoRemainingValueYet()
    {
        var tasks = new[] { new BurndownTask(4, Day(0)) };
        var wednesday = Start.AddDays(2);

        var result = BurndownCalculator.Calculate(tasks, Start, End, wednesday, sprintFinished: false);

        result.Points.Select(p => p.Remaining).Should().Equal(0, 0, 0, null, null);
    }

    [Fact]
    public void FinishedSprint_ShowsEveryDay_EvenIfTodayIsPastIt()
    {
        var tasks = new[] { new BurndownTask(4, Day(2)) };

        var result = BurndownCalculator.Calculate(tasks, Start, End, End.AddDays(30), sprintFinished: true);

        result.Points.Select(p => p.Remaining).Should().Equal(4, 4, 0, 0, 0);
    }

    [Fact]
    public void WithoutAnyStoryPoints_EachTaskCountsAsOne()
    {
        var tasks = new[]
        {
            new BurndownTask(null, Day(0)),
            new BurndownTask(null, null),
            new BurndownTask(null, null)
        };

        var result = BurndownCalculator.Calculate(tasks, Start, End, End, sprintFinished: false);

        result.Unit.Should().Be("tasks");
        result.Total.Should().Be(3);
        result.Points[0].Remaining.Should().Be(2);
    }

    [Fact]
    public void WhenSomeTasksHavePoints_UnestimatedTasksCountAsZero()
    {
        var tasks = new[] { new BurndownTask(8, null), new BurndownTask(null, Day(1)) };

        var result = BurndownCalculator.Calculate(tasks, Start, End, End, sprintFinished: false);

        result.Total.Should().Be(8);
        result.Points[4].Remaining.Should().Be(8, "finishing a task with no points doesn't move the chart");
    }

    [Fact]
    public void TaskCompletedBeforeTheSprintStarted_CountsAsDoneFromDayOne()
    {
        var tasks = new[] { new BurndownTask(5, Start.AddDays(-3)), new BurndownTask(5, null) };

        var result = BurndownCalculator.Calculate(tasks, Start, End, End, sprintFinished: false);

        result.Points[0].Remaining.Should().Be(5);
    }

    [Fact]
    public void EmptySprint_IsAFlatZeroLine()
    {
        var result = BurndownCalculator.Calculate(Array.Empty<BurndownTask>(), Start, End, End, sprintFinished: false);

        result.Total.Should().Be(0);
        result.Points.Should().OnlyContain(p => p.Ideal == 0 && p.Remaining == 0);
    }

    [Fact]
    public void SingleDaySprint_DoesNotDivideByZero()
    {
        var result = BurndownCalculator.Calculate(new[] { new BurndownTask(3, null) }, Start, Start, Start, sprintFinished: false);

        result.Points.Should().ContainSingle();
        result.Points[0].Ideal.Should().Be(0);
    }
}
