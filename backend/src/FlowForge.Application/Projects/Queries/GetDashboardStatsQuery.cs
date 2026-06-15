using MediatR;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Projects.Queries;

public record GetDashboardStatsQuery : IRequest<Result<DashboardStatsDto>>;

public class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetDashboardStatsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<DashboardStatsDto>> Handle(GetDashboardStatsQuery req, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<DashboardStatsDto>(Error.Unauthorized("Auth.NoTenant", "No tenant"));

        var projects = (await _uow.Projects.GetByTenantAsync(_currentUser.TenantId.Value, false, ct)).ToList();
        var myTasks  = (await _uow.Tasks.GetByAssigneeAsync(_currentUser.UserId.Value, _currentUser.TenantId.Value, ct)).ToList();

        var now       = DateTime.UtcNow;
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
        var weekEnd   = weekStart.AddDays(7);

        var stats = new DashboardStatsDto(
            TotalProjects:    projects.Count,
            ActiveProjects:   projects.Count(p => p.Status == Domain.Projects.Enums.ProjectStatus.Active),
            MyOpenTasks:      myTasks.Count(t => !t.IsCompleted),
            MyOverdueTasks:   myTasks.Count(t => t.IsOverdue),
            TasksDueThisWeek: myTasks.Count(t => t.DueDate.HasValue && t.DueDate >= weekStart && t.DueDate < weekEnd),
            CompletedThisWeek: myTasks.Count(t => t.CompletedAt.HasValue && t.CompletedAt >= weekStart)
        );

        return Result.Success(stats);
    }
}
