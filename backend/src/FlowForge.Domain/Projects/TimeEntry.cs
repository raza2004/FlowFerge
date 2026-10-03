using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

/// <summary>
/// One block of time someone spent on a task. The task keeps a running ActualHours total for
/// cheap display; these entries are the history behind it (who, how long, which day, why).
/// </summary>
public sealed class TimeEntry : TenantEntity
{
    public const double MaxHoursPerEntry = 24;

    public Guid TaskId { get; private set; }
    public Guid UserId { get; private set; }
    public double Hours { get; private set; }
    public DateTime WorkDate { get; private set; }
    public string? Note { get; private set; }

    private TimeEntry() { }

    public static Result<TimeEntry> Create(Guid tenantId, Guid taskId, Guid userId, double hours, DateTime workDate, string? note)
    {
        if (hours <= 0 || hours > MaxHoursPerEntry)
            return Result.Failure<TimeEntry>(Error.Validation("TimeEntry.InvalidHours", $"Hours must be more than 0 and at most {MaxHoursPerEntry}"));

        if (note?.Length > 500)
            return Result.Failure<TimeEntry>(Error.Validation("TimeEntry.NoteTooLong", "Note must be at most 500 characters"));

        var entry = new TimeEntry
        {
            TaskId = taskId,
            UserId = userId,
            Hours = Math.Round(hours, 2),
            WorkDate = DateTime.SpecifyKind(workDate.Date, DateTimeKind.Utc),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        entry.TenantId = tenantId;
        return Result.Success(entry);
    }
}
