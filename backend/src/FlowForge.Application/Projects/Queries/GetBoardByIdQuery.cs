using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Application.Projects.Mapping;
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

        var lists = await TaskCardMapper.MapListsAsync(board, _uow, ct);

        return Result.Success(new BoardDto(
            board.Id, board.Name, board.Description, board.Type.ToString(), lists
        ));
    }
}
