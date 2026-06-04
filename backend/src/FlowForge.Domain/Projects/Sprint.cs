using FlowForge.Domain.Projects.Enums;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class Sprint : SoftDeletableEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Goal { get; private set; }
    public SprintStatus Status { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? RetrospectiveNotes { get; private set; }
    public Guid CreatedById { get; private set; }

    public int DurationDays => (EndDate - StartDate).Days;

    private Sprint() { }

    public static Result<Sprint> Create(
        Guid tenantId,
        Guid projectId,
        string name,
        DateTime startDate,
        DateTime endDate,
        Guid createdById,
        string? goal = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure<Sprint>(Error.Validation("Sprint.InvalidName", "Name must be 1-200 chars"));

        if (endDate <= startDate)
            return Result.Failure<Sprint>(Error.Validation("Sprint.InvalidDates", "End date must be after start date"));

        if ((endDate - startDate).TotalDays > 60)
            return Result.Failure<Sprint>(Error.Validation("Sprint.TooLong", "Sprint cannot exceed 60 days"));

        var sprint = new Sprint
        {
            ProjectId = projectId,
            Name = name.Trim(),
            Goal = goal,
            StartDate = startDate,
            EndDate = endDate,
            Status = SprintStatus.Planning,
            CreatedById = createdById
        };

        sprint.GetType().GetProperty("TenantId")!.SetValue(sprint, tenantId);
        return Result.Success(sprint);
    }

    public Result Start()
    {
        if (Status != SprintStatus.Planning)
            return Result.Failure(Error.Conflict("Sprint.CannotStart", "Sprint must be in Planning to start"));

        Status = SprintStatus.Active;
        StartedAt = DateTime.UtcNow;
        Touch();
        return Result.Success();
    }

    public Result Complete(string? retrospectiveNotes = null)
    {
        if (Status != SprintStatus.Active)
            return Result.Failure(Error.Conflict("Sprint.CannotComplete", "Sprint must be Active to complete"));

        Status = SprintStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        RetrospectiveNotes = retrospectiveNotes;
        Touch();
        return Result.Success();
    }

    public Result Cancel()
    {
        Status = SprintStatus.Cancelled;
        Touch();
        return Result.Success();
    }

    public Result UpdateDetails(string name, string? goal, DateTime startDate, DateTime endDate)
    {
        if (Status == SprintStatus.Completed || Status == SprintStatus.Cancelled)
            return Result.Failure(Error.Conflict("Sprint.Frozen", "Cannot edit completed/cancelled sprint"));

        Name = name.Trim();
        Goal = goal;
        StartDate = startDate;
        EndDate = endDate;
        Touch();
        return Result.Success();
    }
}
