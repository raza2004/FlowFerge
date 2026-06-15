using MediatR;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;

namespace FlowForge.Application.Projects.Queries;

public record GetMyTasksQuery : IRequest<Result<List<MyTaskDto>>>;

public class GetMyTasksQueryHandler
    : IRequestHandler<GetMyTasksQuery, Result<List<MyTaskDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetMyTasksQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<MyTaskDto>>> Handle(GetMyTasksQuery req, CancellationToken ct)
    {
        if (_currentUser.TenantId == null || _currentUser.UserId == null)
            return Result.Failure<List<MyTaskDto>>(Error.Unauthorized("Auth.NoTenant", "No tenant"));

        var tasks    = await _uow.Tasks.GetByAssigneeAsync(_currentUser.UserId.Value, _currentUser.TenantId.Value, ct);
        var projects = (await _uow.Projects.GetByTenantAsync(_currentUser.TenantId.Value, false, ct))
            .ToDictionary(p => p.Id);

        var result = tasks.Select(t =>
        {
            projects.TryGetValue(t.ProjectId, out var p);
            return new MyTaskDto(
                t.Id, t.TaskNumber, t.Title,
                p?.Name ?? "Unknown",
                p?.Key.Value ?? "",
                p?.Color ?? "#94a3b8",
                t.Type.ToString(),
                t.Priority.ToString(),
                t.Status,
                t.DueDate,
                t.IsOverdue,
                t.CreatedAt
            );
        }).OrderBy(t => t.DueDate ?? DateTime.MaxValue).ToList();

        return Result.Success(result);
    }
}
