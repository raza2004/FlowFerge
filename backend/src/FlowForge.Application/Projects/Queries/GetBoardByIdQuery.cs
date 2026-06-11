using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Queries;

public record GetBoardByIdQuery(Guid BoardId) : IRequest<Result<BoardDto>>;

public class GetBoardByIdQueryHandler : IRequestHandler<GetBoardByIdQuery, Result<BoardDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetBoardByIdQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<BoardDto>> Handle(GetBoardByIdQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId == null)
            return Result.Failure<BoardDto>(Error.Unauthorized("Auth.NoTenant", "No active tenant"));

        var board = await _uow.Boards.GetByIdWithListsAsync(request.BoardId, ct);
        if (board == null || board.TenantId != _currentUser.TenantId.Value)
            return Result.Failure<BoardDto>(Error.NotFound("Board.NotFound", "Board not found"));

        var lists = board.Lists
            .OrderBy(l => l.Position)
            .Select(l => new BoardListDto(
                l.Id, l.Name, l.Color, l.Position, l.WipLimit,
                l.Tasks
                    .OrderBy(t => t.Position)
                    .Select(t => new TaskCardDto(
                        t.Id, t.TaskNumber, t.Title,
                        t.Type.ToString(), t.Priority.ToString(),
                        t.AssigneeId, null,
                        t.DueDate, t.IsOverdue,
                        t.Position, t.CommentCount
                    ))
                    .ToList()
            ))
            .ToList();

        return Result.Success(new BoardDto(
            board.Id, board.Name, board.Description, board.Type.ToString(), lists
        ));
    }
}
