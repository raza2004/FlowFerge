using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Projects.Repositories;

namespace FlowForge.Domain.Common;

public interface IUnitOfWork
{
    // Identity context repositories
    ITenantRepository Tenants { get; }
    IUserRepository Users { get; }
    IMembershipRepository Memberships { get; }
    IRefreshTokenRepository RefreshTokens { get; }

    // Projects context repositories
    IProjectRepository Projects { get; }
    IBoardRepository Boards { get; }
    ITaskRepository Tasks { get; }
    ISprintRepository Sprints { get; }
    ITaskCommentRepository TaskComments { get; }
    ILabelRepository Labels { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
