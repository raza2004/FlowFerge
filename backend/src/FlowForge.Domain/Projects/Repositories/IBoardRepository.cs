namespace FlowForge.Domain.Projects.Repositories;

public interface IBoardRepository
{
    Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Board?> GetByIdWithListsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Board>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Board board, CancellationToken ct = default);
    void Update(Board board);

    Task<BoardList?> GetListByIdAsync(Guid listId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new list explicitly. Entity ids are generated in C#, so a new list reached only
    /// through an already-loaded board's collection could be mistaken for an existing row.
    /// </summary>
    Task AddListAsync(BoardList list, CancellationToken ct = default);
}
