namespace FlowForge.Domain.Projects.Repositories;

public interface IBoardRepository
{
    Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Board?> GetByIdWithListsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Board>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Board board, CancellationToken ct = default);
    void Update(Board board);
}
