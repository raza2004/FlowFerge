using FlowForge.Domain.Projects.Enums;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class Board : SoftDeletableEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public BoardType Type { get; private set; }
    public int Position { get; private set; }
    public bool IsDefault { get; private set; }
    public Guid CreatedById { get; private set; }

    private readonly List<BoardList> _lists = new();
    public IReadOnlyCollection<BoardList> Lists => _lists.AsReadOnly();

    private Board() { }

    public static Result<Board> Create(
        Guid projectId,
        Guid tenantId,
        string name,
        BoardType type,
        Guid createdById,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure<Board>(Error.Validation("Board.InvalidName", "Name must be 1-100 chars"));

        var board = new Board
        {
            ProjectId = projectId,
            Name = name.Trim(),
            Type = type,
            IsDefault = isDefault,
            CreatedById = createdById
        };

        board.GetType().GetProperty("TenantId")!.SetValue(board, tenantId);
        return Result.Success(board);
    }

    public Result UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure(Error.Validation("Board.InvalidName", "Name must be 1-100 chars"));

        Name = name.Trim();
        Description = description;
        Touch();
        return Result.Success();
    }

    public Result AddList(BoardList list)
    {
        list.GetType().GetProperty("Position")!.SetValue(list, _lists.Count);
        _lists.Add(list);
        Touch();
        return Result.Success();
    }

    public Result ReorderLists(List<Guid> orderedListIds)
    {
        for (int i = 0; i < orderedListIds.Count; i++)
        {
            var list = _lists.FirstOrDefault(l => l.Id == orderedListIds[i]);
            if (list != null) list.SetPosition(i);
        }
        Touch();
        return Result.Success();
    }
}
