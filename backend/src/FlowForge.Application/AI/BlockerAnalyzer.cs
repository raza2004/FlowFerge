using FlowForge.Application.AI.DTOs;

namespace FlowForge.Application.AI;

public record BlockerTaskInfo(
    Guid Id,
    string TaskNumber,
    string Title,
    bool IsCompleted,
    DateTime? DueDate,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime UpdatedAt);

/// <summary>
/// Finds work that's at risk using plain rules, so the findings are explainable, repeatable
/// and free. The AI only reads these findings afterwards (to prioritise and advise); it never
/// decides what counts as a blocker, so a model outage or a hallucination can't hide a real one.
/// </summary>
public static class BlockerAnalyzer
{
    public const int StaleAfterDays = 7;
    public const int VeryStaleAfterDays = 14;
    public const int DueSoonDays = 3;
    public const int OverloadedAt = 8;
    public const int VeryOverloadedAt = 12;

    public const string Overdue = "Overdue";
    public const string DueSoonUnassigned = "DueSoonUnassigned";
    public const string Stale = "Stale";
    public const string Overloaded = "Overloaded";

    private static readonly string[] SeverityOrder = { "High", "Medium", "Low" };

    public static List<BlockerSignalDto> Analyze(IReadOnlyCollection<BlockerTaskInfo> tasks, DateTime now)
    {
        var today = now.Date;
        var open = tasks.Where(t => !t.IsCompleted).ToList();
        var signals = new List<BlockerSignalDto>();
        var flagged = new HashSet<Guid>();

        foreach (var t in open)
        {
            if (t.DueDate is { } due && due.Date < today)
            {
                var days = (today - due.Date).Days;
                signals.Add(ForTask(t, Overdue, days >= 7 ? "High" : "Medium",
                    $"Overdue by {Plural(days, "day")}{Owner(t)}."));
                flagged.Add(t.Id);
            }
        }

        foreach (var t in open)
        {
            if (t.AssigneeId != null || t.DueDate is not { } due) continue;
            var daysLeft = (due.Date - today).Days;
            if (daysLeft is < 0 or > DueSoonDays) continue;

            signals.Add(ForTask(t, DueSoonUnassigned, daysLeft <= 1 ? "High" : "Medium",
                daysLeft == 0 ? "Due today and nobody is assigned." : $"Due in {Plural(daysLeft, "day")} and nobody is assigned."));
            flagged.Add(t.Id);
        }

        // Someone owns it, nothing has happened for a while, and it isn't already flagged above.
        foreach (var t in open)
        {
            if (flagged.Contains(t.Id) || t.AssigneeId == null) continue;
            var idleDays = (today - t.UpdatedAt.Date).Days;
            if (idleDays < StaleAfterDays) continue;

            signals.Add(ForTask(t, Stale, idleDays >= VeryStaleAfterDays ? "Medium" : "Low",
                $"No activity for {Plural(idleDays, "day")}{Owner(t)}."));
        }

        foreach (var group in open.Where(t => t.AssigneeId != null).GroupBy(t => t.AssigneeId!.Value))
        {
            if (group.Count() < OverloadedAt) continue;
            var name = group.First().AssigneeName ?? "A teammate";
            signals.Add(new BlockerSignalDto(null, null, name, Overloaded,
                group.Count() >= VeryOverloadedAt ? "High" : "Medium",
                $"{name} has {group.Count()} open tasks assigned."));
        }

        return signals
            .OrderBy(s => Array.IndexOf(SeverityOrder, s.Severity))
            .ThenBy(s => s.Kind)
            .ThenBy(s => s.TaskNumber, StringComparer.Ordinal)
            .ToList();
    }

    private static BlockerSignalDto ForTask(BlockerTaskInfo t, string kind, string severity, string detail) =>
        new(t.Id, t.TaskNumber, t.Title, kind, severity, detail);

    private static string Owner(BlockerTaskInfo t) => t.AssigneeName is { } name ? $" (assigned to {name})" : "";

    private static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";
}
