using FlowForge.Domain.Common;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Projects.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace FlowForge.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly FlowForgeDbContext _ctx;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(
        FlowForgeDbContext ctx,
        ITenantRepository tenants,
        IUserRepository users,
        IMembershipRepository memberships,
        IRefreshTokenRepository refreshTokens,
        IProjectRepository projects,
        IBoardRepository boards,
        ITaskRepository tasks,
        ISprintRepository sprints,
        ITaskCommentRepository taskComments,
        ILabelRepository labels)
    {
        _ctx = ctx;
        Tenants = tenants;
        Users = users;
        Memberships = memberships;
        RefreshTokens = refreshTokens;
        Projects = projects;
        Boards = boards;
        Tasks = tasks;
        Sprints = sprints;
        TaskComments = taskComments;
        Labels = labels;
    }

    public ITenantRepository Tenants { get; }
    public IUserRepository Users { get; }
    public IMembershipRepository Memberships { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IProjectRepository Projects { get; }
    public IBoardRepository Boards { get; }
    public ITaskRepository Tasks { get; }
    public ISprintRepository Sprints { get; }
    public ITaskCommentRepository TaskComments { get; }
    public ILabelRepository Labels { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default) =>
        _transaction = await _ctx.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose() => _transaction?.Dispose();
}
