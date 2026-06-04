namespace FlowForge.Domain.Projects.Repositories;

public interface ILabelRepository
{
    Task<Label?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Label>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Label label, CancellationToken ct = default);
    void Update(Label label);
}
