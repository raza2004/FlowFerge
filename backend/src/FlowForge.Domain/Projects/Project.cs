using FlowForge.Domain.Projects.Enums;
using FlowForge.Domain.Projects.Events;
using FlowForge.Domain.Projects.ValueObjects;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class Project : SoftDeletableEntity
{
    public string Name { get; private set; } = string.Empty;
    public ProjectKey Key { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? IconUrl { get; private set; }
    public string Color { get; private set; } = "#6366f1";
    public ProjectStatus Status { get; private set; }
    public ProjectVisibility Visibility { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public Guid CreatedById { get; private set; }

    private readonly List<Board> _boards = new();
    public IReadOnlyCollection<Board> Boards => _boards.AsReadOnly();

    private readonly List<ProjectMember> _members = new();
    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    private readonly List<Label> _labels = new();
    public IReadOnlyCollection<Label> Labels => _labels.AsReadOnly();

    private Project() { }

    public static Result<Project> Create(
        Guid tenantId,
        string name,
        ProjectKey key,
        Guid ownerId,
        Guid createdById,
        ProjectVisibility visibility = ProjectVisibility.TenantWide)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure<Project>(Error.Validation("Project.InvalidName", "Name must be 1-200 chars"));

        var project = new Project
        {
            Name = name.Trim(),
            Key = key,
            OwnerId = ownerId,
            CreatedById = createdById,
            Status = ProjectStatus.Active,
            Visibility = visibility
        };

        project.GetType().GetProperty("TenantId")!.SetValue(project, tenantId);
        project.RaiseDomainEvent(new ProjectCreatedEvent(project.Id, tenantId, name, createdById));
        return Result.Success(project);
    }

    public Result UpdateDetails(string name, string? description, string? color, DateTime? startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure(Error.Validation("Project.InvalidName", "Name must be 1-200 chars"));

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            return Result.Failure(Error.Validation("Project.InvalidDates", "End date cannot be before start date"));

        Name = name.Trim();
        Description = description;
        if (!string.IsNullOrWhiteSpace(color)) Color = color;
        StartDate = startDate;
        EndDate = endDate;
        Touch();
        return Result.Success();
    }

    public Result Archive()
    {
        if (Status == ProjectStatus.Archived)
            return Result.Failure(Error.Conflict("Project.AlreadyArchived", "Project already archived"));

        Status = ProjectStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
        Touch();
        return Result.Success();
    }

    public Result Reactivate()
    {
        Status = ProjectStatus.Active;
        ArchivedAt = null;
        Touch();
        return Result.Success();
    }

    public Result ChangeVisibility(ProjectVisibility visibility)
    {
        Visibility = visibility;
        Touch();
        return Result.Success();
    }

    public Result TransferOwnership(Guid newOwnerId)
    {
        if (newOwnerId == Guid.Empty)
            return Result.Failure(Error.Validation("Project.InvalidOwner", "Owner ID required"));

        OwnerId = newOwnerId;
        Touch();
        return Result.Success();
    }

    public Result AddMember(Guid userId, ProjectMemberRole role)
    {
        if (_members.Any(m => m.UserId == userId))
            return Result.Failure(Error.Conflict("Project.MemberExists", "User is already a member"));

        _members.Add(ProjectMember.Create(Id, userId, role));
        Touch();
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result.Failure(Error.NotFound("Project.MemberNotFound", "User is not a member"));

        if (userId == OwnerId)
            return Result.Failure(Error.Forbidden("Project.CannotRemoveOwner", "Cannot remove project owner"));

        _members.Remove(member);
        Touch();
        return Result.Success();
    }
}

public enum ProjectMemberRole
{
    Admin = 0,
    Member = 1,
    Viewer = 2
}
