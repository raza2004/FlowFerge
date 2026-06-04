using FlowForge.Domain.Projects;
using FlowForge.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Infrastructure.Persistence.Repositories;

public class BoardRepository : IBoardRepository
{
    private readonly FlowForgeDbContext _ctx;
    public BoardRepository(FlowForgeDbContext ctx) => _ctx = ctx;

    public Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Boards.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Board?> GetByIdWithListsAsync(Guid id, CancellationToken ct = default) =>
        _ctx.Boards.Include(b => b.Lists).ThenInclude(l => l.Tasks).FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IEnumerable<Board>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await _ctx.Boards.Where(b => b.ProjectId == projectId).OrderBy(b => b.Position).ToListAsync(ct);

    public async Task AddAsync(Board board, CancellationToken ct = default) =>
        await _ctx.Boards.AddAsync(board, ct);

    public void Update(Board board) => _ctx.Boards.Update(board);
}
