using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Queries;

public record GetProjectsQuery(bool IncludeArchived = false) : IRequest<Result<List<ProjectSummaryDto>>>;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, Result<List<ProjectSummaryDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProjectsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ProjectSummaryDto>>> Handle(GetProjectsQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<ProjectSummaryDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var projects = await _uow.Projects.GetByTenantAsync(_currentUser.TenantId.Value, request.IncludeArchived, ct);

        var result = new List<ProjectSummaryDto>();
        foreach (var p in projects)
        {
            var tasks = await _uow.Tasks.GetByProjectAsync(p.Id, ct);
            var taskList = tasks.ToList();
            var completed = taskList.Count(t => t.IsCompleted);

            result.Add(new ProjectSummaryDto(
                p.Id, p.Name, p.Key.Value, p.Color, p.Status.ToString(),
                taskList.Count - completed, completed
            ));
        }

        return Result.Success(result);
    }
}
