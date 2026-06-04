using FlowForge.Shared.Primitives;

namespace FlowForge.Domain.Projects;

public sealed class ProjectMember : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectMemberRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private ProjectMember() { }

    public static ProjectMember Create(Guid projectId, Guid userId, ProjectMemberRole role)
    {
        return new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
    }

    public void ChangeRole(ProjectMemberRole role)
    {
        Role = role;
        Touch();
    }
}
