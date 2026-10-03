using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.Attachments;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Queries;

public record GetTaskDetailQuery(Guid TaskId) : IRequest<Result<TaskDetailDto>>;

public class GetTaskDetailQueryHandler : IRequestHandler<GetTaskDetailQuery, Result<TaskDetailDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetTaskDetailQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<TaskDetailDto>> Handle(GetTaskDetailQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<TaskDetailDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var tenantId = _currentUser.TenantId.Value;
        var task = await _uow.Tasks.GetByIdWithDetailsAsync(request.TaskId, ct);
        if (task == null || task.TenantId != tenantId)
            return Result.Failure<TaskDetailDto>(Error.NotFound("Task.NotFound", "Task not found"));

        var timeEntryRows = (await _uow.Tasks.GetTimeEntriesAsync(task.Id, ct)).ToList();

        var userIds = new List<Guid> { task.CreatedById };
        if (task.AssigneeId.HasValue) userIds.Add(task.AssigneeId.Value);
        userIds.AddRange(task.Watchers.Select(w => w.UserId));
        userIds.AddRange(task.Comments.Select(c => c.AuthorId));
        userIds.AddRange(timeEntryRows.Select(e => e.UserId));
        userIds.AddRange(task.Attachments.Select(a => a.UploadedById));
        var users = (await _uow.Users.GetByIdsAsync(userIds, ct)).ToDictionary(u => u.Id, u => u.FullName);
        string NameOf(Guid id) => users.TryGetValue(id, out var name) ? name : "Unknown user";

        var projectLabels = (await _uow.Labels.GetByProjectAsync(task.ProjectId, ct)).ToDictionary(l => l.Id);

        var labels = task.Labels
            .Where(tl => projectLabels.ContainsKey(tl.LabelId))
            .Select(tl => projectLabels[tl.LabelId])
            .OrderBy(l => l.Name)
            .Select(l => new LabelDto(l.Id, l.Name, l.Color))
            .ToList();

        var watchers = task.Watchers
            .Select(w => new TaskWatcherDto(w.UserId, NameOf(w.UserId)))
            .OrderBy(w => w.FullName)
            .ToList();

        var subtasks = task.Subtasks
            .OrderBy(s => s.CreatedAt)
            .Select(s => new SubtaskDto(s.Id, s.TaskNumber, s.Title, s.IsCompleted))
            .ToList();

        var comments = task.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TaskCommentDto(
                c.Id, c.AuthorId, NameOf(c.AuthorId), c.Content, c.IsEdited, c.CreatedAt, c.MentionedUserIds.ToList()))
            .ToList();

        var timeEntries = timeEntryRows
            .Select(e => new TimeEntryDto(e.Id, e.UserId, NameOf(e.UserId), e.Hours, e.WorkDate, e.Note, e.CreatedAt))
            .ToList();

        return Result.Success(new TaskDetailDto(
            task.Id, task.ProjectId, task.BoardId, task.ListId, task.TaskNumber, task.Title, task.Description,
            task.Type.ToString(), task.Priority.ToString(), task.Status,
            task.AssigneeId, task.AssigneeId.HasValue ? NameOf(task.AssigneeId.Value) : null,
            task.ParentTaskId, task.DueDate, task.EstimatedHours, task.StoryPoints,
            task.IsOverdue, task.IsCompleted, task.CreatedAt, NameOf(task.CreatedById),
            labels, watchers, watchers.Any(w => w.UserId == _currentUser.UserId.Value), subtasks, comments,
            task.ActualHours, timeEntries,
            task.Attachments
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSizeBytes, a.UploadedById,
                    NameOf(a.UploadedById), a.CreatedAt, AttachmentRules.IsPreviewableImage(a.ContentType)))
                .ToList()));
    }
}
