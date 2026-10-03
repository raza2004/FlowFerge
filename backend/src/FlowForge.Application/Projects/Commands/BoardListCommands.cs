using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Projects.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Projects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Projects.Commands;

public record CreateListCommand(Guid BoardId, string Name, string Color, int? WipLimit) : IRequest<Result<BoardListDto>>;
public record UpdateListCommand(Guid ListId, string Name, string Color, int? WipLimit, bool IsDoneColumn) : IRequest<Result>;

/// <summary>Deletes a list; any tasks still in it are moved to <paramref name="MoveTasksToListId"/> first.</summary>
public record DeleteListCommand(Guid ListId, Guid? MoveTasksToListId) : IRequest<Result>;

public record ReorderListsCommand(Guid BoardId, List<Guid> ListIds) : IRequest<Result>;

public class CreateListCommandValidator : AbstractValidator<CreateListCommand>
{
    public CreateListCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$").WithMessage("Color must be a hex value like #6366f1");
        RuleFor(x => x.WipLimit).InclusiveBetween(1, 999).When(x => x.WipLimit.HasValue);
    }
}

public class UpdateListCommandValidator : AbstractValidator<UpdateListCommand>
{
    public UpdateListCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$").WithMessage("Color must be a hex value like #6366f1");
        RuleFor(x => x.WipLimit).InclusiveBetween(1, 999).When(x => x.WipLimit.HasValue);
    }
}

public class BoardListCommandHandlers :
    IRequestHandler<CreateListCommand, Result<BoardListDto>>,
    IRequestHandler<UpdateListCommand, Result>,
    IRequestHandler<DeleteListCommand, Result>,
    IRequestHandler<ReorderListsCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public BoardListCommandHandlers(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    private Error? NoTenant => _currentUser.TenantId == null || _currentUser.UserId == null
        ? Error.Unauthorized("Auth.NoTenant", "No active tenant")
        : null;

    private async Task<Board?> LoadBoardAsync(Guid boardId, CancellationToken ct)
    {
        var board = await _uow.Boards.GetByIdWithListsAsync(boardId, ct);
        return board != null && board.TenantId == _currentUser.TenantId ? board : null;
    }

    public async Task<Result<BoardListDto>> Handle(CreateListCommand request, CancellationToken ct)
    {
        if (NoTenant is { } err) return Result.Failure<BoardListDto>(err);

        var board = await LoadBoardAsync(request.BoardId, ct);
        if (board == null) return Result.Failure<BoardListDto>(Error.NotFound("Board.NotFound", "Board not found"));

        var listResult = BoardList.Create(board.Id, board.TenantId, request.Name, request.Color, request.WipLimit);
        if (listResult.IsFailure) return Result.Failure<BoardListDto>(listResult.Error);

        var list = listResult.Value;
        list.SetPosition(board.Lists.Count == 0 ? 0 : board.Lists.Max(l => l.Position) + 1);
        await _uow.Boards.AddListAsync(list, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new BoardListDto(list.Id, list.Name, list.Color, list.Position, list.WipLimit, new List<TaskCardDto>(), list.IsDoneColumn));
    }

    public async Task<Result> Handle(UpdateListCommand request, CancellationToken ct)
    {
        if (NoTenant is { } err) return Result.Failure(err);

        var list = await _uow.Boards.GetListByIdAsync(request.ListId, ct);
        if (list == null || list.TenantId != _currentUser.TenantId)
            return Result.Failure(Error.NotFound("BoardList.NotFound", "List not found"));

        var updateResult = list.UpdateDetails(request.Name, request.Color, request.WipLimit, request.IsDoneColumn);
        if (updateResult.IsFailure) return updateResult;

        // A task's Status mirrors the name of the list it's in, and "done" follows the list's
        // done flag - so renaming a list or toggling its done flag updates every task in it.
        foreach (var task in list.Tasks)
            task.ChangeStatus(list.Name, list.IsDoneColumn, _currentUser.UserId!.Value, list.TenantId);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(DeleteListCommand request, CancellationToken ct)
    {
        if (NoTenant is { } err) return Result.Failure(err);
        var userId = _currentUser.UserId!.Value;

        var list = await _uow.Boards.GetListByIdAsync(request.ListId, ct);
        if (list == null || list.TenantId != _currentUser.TenantId)
            return Result.Failure(Error.NotFound("BoardList.NotFound", "List not found"));

        var board = await LoadBoardAsync(list.BoardId, ct);
        if (board == null) return Result.Failure(Error.NotFound("Board.NotFound", "Board not found"));

        if (board.Lists.Count <= 1)
            return Result.Failure(Error.Validation("BoardList.LastList", "A board needs at least one list"));

        var tasks = list.Tasks.OrderBy(t => t.Position).ToList();
        if (tasks.Count > 0)
        {
            var target = board.Lists.FirstOrDefault(l => l.Id == request.MoveTasksToListId && l.Id != list.Id);
            if (target == null)
                return Result.Failure(Error.Validation("BoardList.NotEmpty",
                    $"This list still has {tasks.Count} task(s). Choose another list on this board to move them to."));

            var offset = target.Tasks.Count;
            for (var i = 0; i < tasks.Count; i++)
            {
                tasks[i].MoveTo(target.Id, offset + i, userId, list.TenantId);
                tasks[i].ChangeStatus(target.Name, target.IsDoneColumn, userId, list.TenantId);
            }
        }

        list.SoftDelete(userId);

        // Rules that trigger on this list could never fire again, so remove them rather than leave dead rules behind.
        var rules = await _uow.AutomationRules.GetByProjectAsync(board.ProjectId, ct);
        foreach (var rule in rules.Where(r => r.TriggerListId == list.Id))
            rule.SoftDelete(userId);

        var position = 0;
        foreach (var remaining in board.Lists.Where(l => l.Id != list.Id).OrderBy(l => l.Position))
            remaining.SetPosition(position++);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(ReorderListsCommand request, CancellationToken ct)
    {
        if (NoTenant is { } err) return Result.Failure(err);

        var board = await LoadBoardAsync(request.BoardId, ct);
        if (board == null) return Result.Failure(Error.NotFound("Board.NotFound", "Board not found"));

        var current = board.Lists.Select(l => l.Id).ToHashSet();
        if (request.ListIds.Count != current.Count || !current.SetEquals(request.ListIds))
            return Result.Failure(Error.Validation("BoardList.InvalidOrder", "The new order must contain every list on the board exactly once"));

        board.ReorderLists(request.ListIds);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
