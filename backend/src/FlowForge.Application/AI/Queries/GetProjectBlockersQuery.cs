using FlowForge.Application.Features;
using FlowForge.Application.AI.DTOs;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.AI.Queries;

public record GetProjectBlockersQuery(Guid ProjectId) : IRequest<Result<ProjectBlockersDto>>, IRequiresFeature
{
    public string FeatureKey => FeatureKeys.AiAssistant;
}

/// <summary>
/// Rule-based risk scan plus an optional AI read of it. If the AI isn't configured or errors,
/// the scan itself is still returned (with AiAvailable = false and the reason) - the findings
/// never depend on the model. With no findings the model isn't called at all.
/// </summary>
public class GetProjectBlockersQueryHandler : IRequestHandler<GetProjectBlockersQuery, Result<ProjectBlockersDto>>
{
    private const int MaxSignalsForAi = 20;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAiService _ai;
    private readonly IDateTimeProvider _clock;

    public GetProjectBlockersQueryHandler(IUnitOfWork uow, ICurrentUser currentUser, IAiService ai, IDateTimeProvider clock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ai = ai;
        _clock = clock;
    }

    public async Task<Result<ProjectBlockersDto>> Handle(GetProjectBlockersQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<ProjectBlockersDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<ProjectBlockersDto>(Error.NotFound("Project.NotFound", "Project not found"));

        var tasks = (await _uow.Tasks.GetByProjectAsync(request.ProjectId, ct)).ToList();
        var assigneeIds = tasks.Where(t => t.AssigneeId.HasValue).Select(t => t.AssigneeId!.Value);
        var names = (await _uow.Users.GetByIdsAsync(assigneeIds, ct)).ToDictionary(u => u.Id, u => u.FullName);

        var now = _clock.UtcNow;
        var signals = BlockerAnalyzer.Analyze(
            tasks.Select(t => new BlockerTaskInfo(
                t.Id, t.TaskNumber, t.Title, t.IsCompleted, t.DueDate, t.AssigneeId,
                t.AssigneeId.HasValue ? names.GetValueOrDefault(t.AssigneeId.Value) : null, t.UpdatedAt)).ToList(),
            now);

        var openTasks = tasks.Count(t => !t.IsCompleted);

        if (signals.Count == 0)
            return Result.Success(new ProjectBlockersDto(signals, openTasks, null, new List<string>(), true,
                "No blockers found. Nothing is overdue, stalled, or piling up on one person.", now));

        var lines = signals.Take(MaxSignalsForAi)
            .Select(s => s.TaskNumber != null ? $"[{s.Severity}] {s.TaskNumber} \"{s.Title}\": {s.Detail}" : $"[{s.Severity}] {s.Detail}")
            .ToList();

        var analysis = await _ai.AnalyzeBlockersAsync(new BlockerAnalysisInput(project.Name, openTasks, lines), ct);
        if (analysis.IsFailure)
        {
            var message = analysis.Error.Code == "AI.NotConfigured"
                ? "AI analysis is off because no AI API key is configured. The findings above come from built-in rules."
                : "The AI analysis is unavailable right now. The findings above come from built-in rules.";
            return Result.Success(new ProjectBlockersDto(signals, openTasks, null, new List<string>(), false, message, now));
        }

        return Result.Success(new ProjectBlockersDto(signals, openTasks, analysis.Value.Summary,
            analysis.Value.Recommendations, true, null, now));
    }
}
