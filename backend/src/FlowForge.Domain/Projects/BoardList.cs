using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class BoardList : SoftDeletableEntity
{
    public Guid BoardId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#94a3b8";
    public int Position { get; private set; }
    public int? WipLimit { get; private set; }
    public bool IsDoneColumn { get; private set; }

    private readonly List<ProjectTask> _tasks = new();
    public IReadOnlyCollection<ProjectTask> Tasks => _tasks.AsReadOnly();

    private BoardList() { }

    public static Result<BoardList> Create(Guid boardId, Guid tenantId, string name, string color = "#94a3b8", int? wipLimit = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure<BoardList>(Error.Validation("BoardList.InvalidName", "Name must be 1-100 chars"));

        var list = new BoardList
        {
            BoardId = boardId,
            Name = name.Trim(),
            Color = color,
            WipLimit = wipLimit
        };

        list.GetType().GetProperty("TenantId")!.SetValue(list, tenantId);
        return Result.Success(list);
    }

    public void SetPosition(int position)
    {
        Position = position;
        Touch();
    }

    public Result UpdateDetails(string name, string color, int? wipLimit, bool isDoneColumn)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Result.Failure(Error.Validation("BoardList.InvalidName", "Name must be 1-100 chars"));

        Name = name.Trim();
        Color = color;
        WipLimit = wipLimit;
        IsDoneColumn = isDoneColumn;
        Touch();
        return Result.Success();
    }

    public bool IsAtWipLimit() => WipLimit.HasValue && _tasks.Count >= WipLimit.Value;
}
