namespace FlowForge.Application.Projects.Sprints;

public record BurndownPoint(DateTime Date, double Ideal, double? Remaining);

public record BurndownDto(string Unit, double Total, List<BurndownPoint> Points);

/// <summary>One top-level sprint task as the burndown sees it.</summary>
public record BurndownTask(int? StoryPoints, DateTime? CompletedAt);

/// <summary>
/// Pure burndown maths (no database, no clock of its own) so it can be tested exactly.
///
/// Scope is the sprint's tasks as they are now, held constant across the whole chart, and
/// a task counts as finished on the day it was completed. The unit is story points when any
/// task has points (tasks without points then count as 0, so unestimated work doesn't skew
/// the chart), otherwise plain task count.
/// </summary>
public static class BurndownCalculator
{
    public static BurndownDto Calculate(
        IReadOnlyCollection<BurndownTask> tasks, DateTime sprintStart, DateTime sprintEnd, DateTime today, bool sprintFinished)
    {
        var usePoints = tasks.Any(t => t.StoryPoints is > 0);
        double Weight(BurndownTask t) => usePoints ? (t.StoryPoints ?? 0) : 1;

        var total = tasks.Sum(Weight);
        var start = sprintStart.Date;
        var end = sprintEnd.Date;
        var dayCount = Math.Max(1, (end - start).Days + 1);

        var points = new List<BurndownPoint>(dayCount);
        for (var i = 0; i < dayCount; i++)
        {
            var day = start.AddDays(i);
            var ideal = dayCount == 1 ? 0 : total * (1 - (double)i / (dayCount - 1));

            // Don't draw "remaining" for days that haven't happened yet.
            double? remaining = null;
            if (sprintFinished || day <= today.Date)
            {
                var done = tasks.Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value.Date <= day).Sum(Weight);
                remaining = total - done;
            }

            points.Add(new BurndownPoint(day, Math.Round(ideal, 2), remaining));
        }

        return new BurndownDto(usePoints ? "points" : "tasks", total, points);
    }
}
