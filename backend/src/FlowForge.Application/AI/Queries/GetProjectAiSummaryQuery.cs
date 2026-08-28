using FlowForge.Application.AI.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.AI.Queries;

public record GetProjectAiSummaryQuery(Guid ProjectId) : IRequest<Result<ProjectAiSummaryDto>>;

public class GetProjectAiSummaryQueryHandler : IRequestHandler<GetProjectAiSummaryQuery, Result<ProjectAiSummaryDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAiService _ai;
    private readonly IDateTimeProvider _clock;

    public GetProjectAiSummaryQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IAiService ai, IDateTimeProvider clock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ai = ai;
        _clock = clock;
    }

    public async Task<Result<ProjectAiSummaryDto>> Handle(GetProjectAiSummaryQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<ProjectAiSummaryDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<ProjectAiSummaryDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var tasks = (await _uow.Tasks.GetByProjectAsync(request.ProjectId, ct)).ToList();
        if (tasks.Count == 0)
            return Result.Failure<ProjectAiSummaryDto>(Error.Validation("Project.NoTasks", "Add some tasks before generating a summary"));

        var input = new ProjectSummaryInput(
            ProjectName: project.Name,
            TotalTasks: tasks.Count,
            CompletedTasks: tasks.Count(t => t.IsCompleted),
            OverdueTasks: tasks.Count(t => t.IsOverdue),
            InProgressTaskTitles: tasks.Where(t => !t.IsCompleted && t.Status != "To Do").Select(t => t.Title).Take(15).ToList(),
            OverdueTaskTitles: tasks.Where(t => t.IsOverdue).Select(t => t.Title).Take(15).ToList()
        );

        var result = await _ai.SummarizeProjectAsync(input, ct);
        if (result.IsFailure) return Result.Failure<ProjectAiSummaryDto>(result.Error);

        return Result.Success(new ProjectAiSummaryDto(result.Value, _clock.UtcNow));
    }
}
