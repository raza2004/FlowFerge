using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;

namespace FlowForge.Application.Projects.Mapping;

/// <summary>
/// Builds board cards (TaskCardDto) for a whole board in one pass: label names/colors and
/// assignee names are looked up once per board instead of once per card.
/// </summary>
public static class TaskCardMapper
{
    public static async Task<List<BoardListDto>> MapListsAsync(Board board, IUnitOfWork uow, CancellationToken ct)
    {
        var labels = (await uow.Labels.GetByProjectAsync(board.ProjectId, ct)).ToDictionary(l => l.Id);

        var allTasks = board.Lists.SelectMany(l => l.Tasks).ToList();
        var assigneeIds = allTasks.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value);
        var names = (await uow.Users.GetByIdsAsync(assigneeIds, ct)).ToDictionary(u => u.Id, u => u.FullName);
        var sprintByTask = allTasks.ToDictionary(t => t.Id, t => t.SprintId);
        var subtaskCounts = allTasks
            .Where(t => t.ParentTaskId.HasValue)
            .GroupBy(t => t.ParentTaskId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return board.Lists
            .OrderBy(l => l.Position)
            .Select(l => new BoardListDto(
                l.Id, l.Name, l.Color, l.Position, l.WipLimit,
                l.Tasks.OrderBy(t => t.Position).Select(t => new TaskCardDto(
                    t.Id, t.TaskNumber, t.Title,
                    t.Type.ToString(), t.Priority.ToString(),
                    t.AssigneeId,
                    t.AssigneeId.HasValue && names.TryGetValue(t.AssigneeId.Value, out var name) ? name : null,
                    t.DueDate, t.IsOverdue,
                    t.Position, t.CommentCount,
                    t.Labels
                        .Where(tl => labels.ContainsKey(tl.LabelId))
                        .Select(tl => labels[tl.LabelId])
                        .OrderBy(label => label.Name)
                        .Select(label => new LabelDto(label.Id, label.Name, label.Color))
                        .ToList(),
                    t.ParentTaskId,
                    subtaskCounts.GetValueOrDefault(t.Id),
                    t.AttachmentCount,
                    // Subtasks have no sprint of their own; they show with their parent's.
                    t.ParentTaskId.HasValue ? sprintByTask.GetValueOrDefault(t.ParentTaskId.Value) : t.SprintId,
                    t.StoryPoints
                )).ToList(),
                l.IsDoneColumn
            ))
            .ToList();
    }
}
