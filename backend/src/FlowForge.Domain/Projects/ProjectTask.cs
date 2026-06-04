using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Events;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class ProjectTask : SoftDeletableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid BoardId { get; private set; }
    public Guid ListId { get; private set; }
    public string TaskNumber { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskType Type { get; private set; }
    public TaskPriority Priority { get; private set; }
    public string Status { get; private set; } = "Todo";
    public int Position { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid? ParentTaskId { get; private set; }
    public Guid? SprintId { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public double? EstimatedHours { get; private set; }
    public double? ActualHours { get; private set; }
    public int? StoryPoints { get; private set; }
    public Guid CreatedById { get; private set; }
    public Guid? ReporterId { get; private set; }
    public int CommentCount { get; private set; }
    public int AttachmentCount { get; private set; }

    private readonly List<ProjectTask> _subtasks = new();
    public IReadOnlyCollection<ProjectTask> Subtasks => _subtasks.AsReadOnly();

    private readonly List<TaskComment> _comments = new();
    public IReadOnlyCollection<TaskComment> Comments => _comments.AsReadOnly();

    private readonly List<TaskAttachment> _attachments = new();
    public IReadOnlyCollection<TaskAttachment> Attachments => _attachments.AsReadOnly();

    private readonly List<TaskLabel> _labels = new();
    public IReadOnlyCollection<TaskLabel> Labels => _labels.AsReadOnly();

    private readonly List<TaskWatcher> _watchers = new();
    public IReadOnlyCollection<TaskWatcher> Watchers => _watchers.AsReadOnly();

    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.UtcNow && CompletedAt == null;
    public bool IsCompleted => CompletedAt.HasValue;

    private ProjectTask() { }

    public static Result<ProjectTask> Create(
        Guid tenantId,
        Guid projectId,
        Guid boardId,
        Guid listId,
        string taskNumber,
        string title,
        TaskType type,
        TaskPriority priority,
        Guid createdById,
        Guid? parentTaskId = null)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 500)
            return Result.Failure<ProjectTask>(Error.Validation("Task.InvalidTitle", "Title must be 1-500 chars"));

        var task = new ProjectTask
        {
            ProjectId = projectId,
            BoardId = boardId,
            ListId = listId,
            TaskNumber = taskNumber,
            Title = title.Trim(),
            Type = type,
            Priority = priority,
            CreatedById = createdById,
            ReporterId = createdById,
            ParentTaskId = parentTaskId,
            Status = "Todo"
        };

        task.GetType().GetProperty("TenantId")!.SetValue(task, tenantId);
        task.RaiseDomainEvent(new TaskCreatedEvent(task.Id, projectId, tenantId, title, createdById));
        return Result.Success(task);
    }

    public Result UpdateDetails(string title, string? description, TaskType type, TaskPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 500)
            return Result.Failure(Error.Validation("Task.InvalidTitle", "Title must be 1-500 chars"));

        Title = title.Trim();
        Description = description;
        Type = type;
        Priority = priority;
        Touch();
        return Result.Success();
    }

    public Result Assign(Guid assigneeId, Guid assignedById, Guid tenantId)
    {
        if (assigneeId == Guid.Empty)
            return Result.Failure(Error.Validation("Task.InvalidAssignee", "Assignee required"));

        AssigneeId = assigneeId;
        Touch();
        RaiseDomainEvent(new TaskAssignedEvent(Id, assigneeId, assignedById, tenantId));
        return Result.Success();
    }

    public Result Unassign()
    {
        AssigneeId = null;
        Touch();
        return Result.Success();
    }

    public Result MoveTo(Guid newListId, int newPosition, Guid movedById, Guid tenantId)
    {
        if (newListId == ListId && newPosition == Position)
            return Result.Success();

        var oldListId = ListId;
        ListId = newListId;
        Position = newPosition;
        Touch();

        if (oldListId != newListId)
            RaiseDomainEvent(new TaskMovedEvent(Id, oldListId, newListId, movedById, tenantId));

        return Result.Success();
    }

    public Result ChangeStatus(string newStatus, Guid changedById, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
            return Result.Failure(Error.Validation("Task.InvalidStatus", "Status required"));

        var oldStatus = Status;
        Status = newStatus;

        if (newStatus.Equals("Done", StringComparison.OrdinalIgnoreCase) || newStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            CompletedAt = DateTime.UtcNow;
        else
            CompletedAt = null;

        Touch();
        RaiseDomainEvent(new TaskStatusChangedEvent(Id, oldStatus, newStatus, changedById, tenantId));
        return Result.Success();
    }

    public Result SetDueDate(DateTime? dueDate)
    {
        DueDate = dueDate;
        Touch();
        return Result.Success();
    }

    public Result SetEstimate(double? hours, int? storyPoints)
    {
        if (hours.HasValue && hours.Value < 0)
            return Result.Failure(Error.Validation("Task.InvalidEstimate", "Hours cannot be negative"));

        EstimatedHours = hours;
        StoryPoints = storyPoints;
        Touch();
        return Result.Success();
    }

    public Result LogTime(double hours)
    {
        if (hours <= 0)
            return Result.Failure(Error.Validation("Task.InvalidHours", "Hours must be positive"));

        ActualHours = (ActualHours ?? 0) + hours;
        Touch();
        return Result.Success();
    }

    public Result AddLabel(Guid labelId)
    {
        if (_labels.Any(l => l.LabelId == labelId))
            return Result.Failure(Error.Conflict("Task.LabelExists", "Label already added"));

        _labels.Add(new TaskLabel { TaskId = Id, LabelId = labelId });
        Touch();
        return Result.Success();
    }

    public Result RemoveLabel(Guid labelId)
    {
        var label = _labels.FirstOrDefault(l => l.LabelId == labelId);
        if (label == null) return Result.Failure(Error.NotFound("Task.LabelNotFound", "Label not on task"));
        _labels.Remove(label);
        Touch();
        return Result.Success();
    }

    public Result AddWatcher(Guid userId)
    {
        if (_watchers.Any(w => w.UserId == userId))
            return Result.Success();

        _watchers.Add(new TaskWatcher { TaskId = Id, UserId = userId });
        Touch();
        return Result.Success();
    }

    public void IncrementCommentCount() => CommentCount++;
    public void DecrementCommentCount() => CommentCount = Math.Max(0, CommentCount - 1);
    public void IncrementAttachmentCount() => AttachmentCount++;
    public void DecrementAttachmentCount() => AttachmentCount = Math.Max(0, AttachmentCount - 1);
}
