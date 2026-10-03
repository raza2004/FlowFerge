using FlowForge.Domain.Projects.Enums;

namespace FlowForge.Application.Projects.DTOs;

public record CreateProjectRequest(
    string Name,
    string Key,
    string? Description,
    string? Color,
    ProjectVisibility Visibility
);

public record UpdateProjectRequest(
    string Name,
    string? Description,
    string? Color,
    DateTime? StartDate,
    DateTime? EndDate
);

public record ProjectDto(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    string Color,
    string Status,
    string Visibility,
    Guid OwnerId,
    string? OwnerName,
    DateTime? StartDate,
    DateTime? EndDate,
    int BoardCount,
    int TaskCount,
    int MemberCount,
    DateTime CreatedAt
);

public record ProjectSummaryDto(
    Guid Id,
    string Name,
    string Key,
    string Color,
    string Status,
    int OpenTasks,
    int CompletedTasks
);

public record CreateBoardRequest(string Name, string? Description, BoardType Type);
public record CreateListRequest(string Name, string Color, int? WipLimit);
public record CreateTaskRequest(
    Guid BoardId,
    Guid ListId,
    string Title,
    string? Description,
    TaskType Type,
    TaskPriority Priority,
    Guid? AssigneeId,
    DateTime? DueDate,
    double? EstimatedHours
);
public record UpdateTaskRequest(
    string Title,
    string? Description,
    TaskType Type,
    TaskPriority Priority,
    DateTime? DueDate,
    double? EstimatedHours,
    int? StoryPoints,
    Guid? BoardId
);

public record LabelDto(Guid Id, string Name, string Color);
public record CreateLabelRequest(string Name, string Color);
public record TaskWatcherDto(Guid UserId, string FullName);
public record SubtaskDto(Guid Id, string TaskNumber, string Title, bool IsCompleted);
public record TaskCommentDto(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    string Content,
    bool IsEdited,
    DateTime CreatedAt,
    List<Guid> MentionedUserIds
);
public record AddCommentRequest(string Content, List<Guid>? MentionedUserIds);
public record UpdateListRequest(string Name, string Color, int? WipLimit, bool IsDoneColumn);
public record ReorderListsRequest(List<Guid> ListIds);
public record LogTimeRequest(double Hours, DateTime? WorkDate, string? Note);
public record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Guid UploadedById,
    string UploadedByName,
    DateTime CreatedAt,
    bool IsPreviewableImage
);
public record TimeEntryDto(Guid Id, Guid UserId, string UserName, double Hours, DateTime WorkDate, string? Note, DateTime CreatedAt);
public record EditCommentRequest(string Content);
public record CreateSubtaskRequest(string Title);

public record TaskDetailDto(
    Guid Id,
    Guid ProjectId,
    Guid BoardId,
    Guid ListId,
    string TaskNumber,
    string Title,
    string? Description,
    string Type,
    string Priority,
    string Status,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? ParentTaskId,
    DateTime? DueDate,
    double? EstimatedHours,
    int? StoryPoints,
    bool IsOverdue,
    bool IsCompleted,
    DateTime CreatedAt,
    string? CreatedByName,
    List<LabelDto> Labels,
    List<TaskWatcherDto> Watchers,
    bool IsWatching,
    List<SubtaskDto> Subtasks,
    List<TaskCommentDto> Comments,
    double? ActualHours,
    List<TimeEntryDto> TimeEntries,
    List<AttachmentDto> Attachments
);
public record MoveTaskRequest(Guid ListId, int Position);
public record TaskDto(
    Guid Id,
    string TaskNumber,
    string Title,
    string? Description,
    string Type,
    string Priority,
    string Status,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime? DueDate,
    double? EstimatedHours,
    double? ActualHours,
    int CommentCount,
    int AttachmentCount,
    bool IsOverdue,
    DateTime CreatedAt
);

public record BoardDto(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    List<BoardListDto> Lists
);

public record BoardListDto(
    Guid Id,
    string Name,
    string Color,
    int Position,
    int? WipLimit,
    List<TaskCardDto> Tasks,
    bool IsDoneColumn = false
);

public record TaskCardDto(
    Guid Id,
    string TaskNumber,
    string Title,
    string Type,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime? DueDate,
    bool IsOverdue,
    int Position,
    int CommentCount,
    List<LabelDto>? Labels = null,
    Guid? ParentTaskId = null,
    int SubtaskCount = 0,
    int AttachmentCount = 0
);

public record DashboardStatsDto(
    int TotalProjects,
    int ActiveProjects,
    int MyOpenTasks,
    int MyOverdueTasks,
    int TasksDueThisWeek,
    int CompletedThisWeek
);

public record MyTaskDto(
    Guid Id,
    string TaskNumber,
    string Title,
    string ProjectName,
    string ProjectKey,
    string ProjectColor,
    string Type,
    string Priority,
    string Status,
    DateTime? DueDate,
    bool IsOverdue,
    DateTime CreatedAt
);

public record ActivityItemDto(
    string Type,
    string Title,
    string Subtitle,
    DateTime At,
    string? IconColor
);
