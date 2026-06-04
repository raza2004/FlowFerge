using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Queries;

public record GetProjectByIdQuery(Guid Id) : IRequest<Result<ProjectDto>>;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, Result<ProjectDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProjectByIdQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<ProjectDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdWithDetailsAsync(request.Id, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<ProjectDto>(Error.NotFound("Project.NotFound", "Project not found"));

        return Result.Success(new ProjectDto(
            project.Id, project.Name, project.Key.Value, project.Description,
            project.Color, project.Status.ToString(), project.Visibility.ToString(),
            project.OwnerId, null, project.StartDate, project.EndDate,
            project.Boards.Count, project.Boards.Sum(b => b.Lists.Sum(l => l.Tasks.Count)),
            project.Members.Count, project.CreatedAt
        ));
    }
}
