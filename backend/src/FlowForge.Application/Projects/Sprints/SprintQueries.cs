using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Sprints;

public record GetSprintsQuery(Guid ProjectId) : IRequest<Result<List<SprintDto>>>;
public record GetSprintDetailQuery(Guid SprintId) : IRequest<Result<SprintDetailDto>>;
public record GetBacklogQuery(Guid ProjectId) : IRequest<Result<List<SprintTaskDto>>>;

internal static class SprintMapper
{
    public static SprintDto ToDto(Sprint s, IReadOnlyCollection<ProjectTask> tasks) => new(
        s.Id, s.Name, s.Goal, s.Status.ToString(), s.StartDate, s.EndDate, s.StartedAt, s.CompletedAt, s.RetrospectiveNotes,
        tasks.Count,
        tasks.Count(t => t.IsCompleted),
        tasks.Sum(t => t.StoryPoints ?? 0),
        tasks.Where(t => t.IsCompleted).Sum(t => t.StoryPoints ?? 0));

    public static async Task<List<SprintTaskDto>> ToTaskDtosAsync(IEnumerable<ProjectTask> tasks, IUnitOfWork uow, CancellationToken ct)
    {
        var list = tasks.ToList();
        var assigneeIds = list.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value);
        var names = (await uow.Users.GetByIdsAsync(assigneeIds, ct)).ToDictionary(u => u.Id, u => u.FullName);

        return list.Select(t => new SprintTaskDto(
            t.Id, t.TaskNumber, t.Title, t.Type.ToString(), t.Priority.ToString(), t.Status, t.IsCompleted, t.StoryPoints,
            t.AssigneeId, t.AssigneeId.HasValue ? names.GetValueOrDefault(t.AssigneeId.Value) : null,
            t.DueDate)).ToList();
    }
}

public class SprintQueryHandlers :
    IRequestHandler<GetSprintsQuery, Result<List<SprintDto>>>,
    IRequestHandler<GetSprintDetailQuery, Result<SprintDetailDto>>,
    IRequestHandler<GetBacklogQuery, Result<List<SprintTaskDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public SprintQueryHandlers(IUnitOfWork uow, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _clock = clock;
    }

    private async Task<Project?> OwnProjectAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(projectId, ct);
        return project != null && project.TenantId == _currentUser.TenantId ? project : null;
    }

    public async Task<Result<List<SprintDto>>> Handle(GetSprintsQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<SprintDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));
        if (await OwnProjectAsync(request.ProjectId, ct) == null)
            return Result.Failure<List<SprintDto>>(Error.NotFound("Project.NotFound", "Project not found"));

        var sprints = (await _uow.Sprints.GetByProjectAsync(request.ProjectId, ct)).ToList();
        var tasks = (await _uow.Tasks.GetBySprintsAsync(sprints.Select(s => s.Id), ct)).ToLookup(t => t.SprintId);

        // Open sprints first (active, then planned), then history newest first.
        return Result.Success(sprints
            .OrderBy(s => s.Status == SprintStatus.Active ? 0 : s.Status == SprintStatus.Planning ? 1 : 2)
            .ThenByDescending(s => s.StartDate)
            .Select(s => SprintMapper.ToDto(s, tasks[s.Id].ToList()))
            .ToList());
    }

    public async Task<Result<SprintDetailDto>> Handle(GetSprintDetailQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<SprintDetailDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var sprint = await _uow.Sprints.GetByIdAsync(request.SprintId, ct);
        if (sprint == null || sprint.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<SprintDetailDto>(Error.NotFound("Sprint.NotFound", "Sprint not found"));

        var tasks = (await _uow.Tasks.GetBySprintAsync(sprint.Id, ct)).OrderBy(t => t.CreatedAt).ToList();
        var finished = sprint.Status is SprintStatus.Completed or SprintStatus.Cancelled;
        var end = sprint.CompletedAt is { } completed && completed < sprint.EndDate ? completed : sprint.EndDate;

        var burndown = BurndownCalculator.Calculate(
            tasks.Select(t => new BurndownTask(t.StoryPoints, t.CompletedAt)).ToList(),
            sprint.StartDate, finished ? end : sprint.EndDate, _clock.UtcNow, finished);

        return Result.Success(new SprintDetailDto(
            SprintMapper.ToDto(sprint, tasks),
            await SprintMapper.ToTaskDtosAsync(tasks, _uow, ct),
            burndown));
    }

    public async Task<Result<List<SprintTaskDto>>> Handle(GetBacklogQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<SprintTaskDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));
        if (await OwnProjectAsync(request.ProjectId, ct) == null)
            return Result.Failure<List<SprintTaskDto>>(Error.NotFound("Project.NotFound", "Project not found"));

        var tasks = await _uow.Tasks.GetBacklogAsync(request.ProjectId, ct);
        return Result.Success(await SprintMapper.ToTaskDtosAsync(tasks, _uow, ct));
    }
}
