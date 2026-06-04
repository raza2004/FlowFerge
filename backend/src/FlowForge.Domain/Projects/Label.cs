using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects;

public sealed class Label : Entity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#6366f1";
    public string? Description { get; private set; }

    private Label() { }

    public static Result<Label> Create(Guid projectId, string name, string color, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
            return Result.Failure<Label>(Error.Validation("Label.InvalidName", "Name must be 1-50 chars"));

        return Result.Success(new Label
        {
            ProjectId = projectId,
            Name = name.Trim(),
            Color = color,
            Description = description
        });
    }

    public Result Update(string name, string color, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
            return Result.Failure(Error.Validation("Label.InvalidName", "Name must be 1-50 chars"));

        Name = name.Trim();
        Color = color;
        Description = description;
        Touch();
        return Result.Success();
    }
}
