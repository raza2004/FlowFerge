using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Queries;

public record GetProjectBoardsQuery(Guid ProjectId) : IRequest<Result<List<BoardDto>>>;

public class GetProjectBoardsQueryHandler : IRequestHandler<GetProjectBoardsQuery, Result<List<BoardDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProjectBoardsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<BoardDto>>> Handle(GetProjectBoardsQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<List<BoardDto>>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project == null || project.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<List<BoardDto>>(Error.NotFound("Project.NotFound", "Project not found"));

        var boards = await _uow.Boards.GetByProjectAsync(request.ProjectId, ct);
        var result = new List<BoardDto>();

        foreach (var b in boards)
        {
            var full = await _uow.Boards.GetByIdWithListsAsync(b.Id, ct);
            if (full == null) continue;

            result.Add(new BoardDto(
                full.Id, full.Name, full.Description, full.Type.ToString(),
                full.Lists.OrderBy(l => l.Position).Select(l => new BoardListDto(
                    l.Id, l.Name, l.Color, l.Position, l.WipLimit,
                    l.Tasks.OrderBy(t => t.Position).Select(t => new TaskCardDto(
                        t.Id, t.TaskNumber, t.Title,
                        t.Type.ToString(), t.Priority.ToString(),
                        t.AssigneeId, null,
                        t.DueDate, t.IsOverdue,
                        t.Position, t.CommentCount
                    )).ToList()
                )).ToList()
            ));
        }

        return Result.Success(result);
    }
}
