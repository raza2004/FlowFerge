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
    TaskPriority Priority
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
    List<TaskCardDto> Tasks
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
    int CommentCount
);
