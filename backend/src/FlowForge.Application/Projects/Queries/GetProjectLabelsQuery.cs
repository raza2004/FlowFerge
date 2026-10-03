using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Queries;

public record GetProjectLabelsQuery(Guid ProjectId) : IRequest<Result<List<LabelDto>>>;

public class GetProjectLabelsQueryHandler : IRequestHandler<GetProjectLabelsQuery, Result<List<LabelDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProjectLabelsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<LabelDto>>> Handle(GetProjectLabelsQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<LabelDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<List<LabelDto>>(Error.NotFound("Project.NotFound", "Project not found"));

        var labels = await _uow.Labels.GetByProjectAsync(request.ProjectId, ct);
        return Result.Success(labels.OrderBy(l => l.Name).Select(l => new LabelDto(l.Id, l.Name, l.Color)).ToList());
    }
}
