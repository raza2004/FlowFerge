using System.Text.RegularExpressions;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Projects.ValueObjects;

public sealed class ProjectKey : ValueObject
{
    public string Value { get; }

    private ProjectKey(string value) => Value = value;

    public static Result<ProjectKey> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<ProjectKey>(Error.Validation("ProjectKey.Empty", "Project key required"));

        if (value.Length < 2 || value.Length > 10)
            return Result.Failure<ProjectKey>(Error.Validation("ProjectKey.Length", "Project key must be 2-10 chars"));

        if (!Regex.IsMatch(value, "^[A-Z][A-Z0-9]*$"))
            return Result.Failure<ProjectKey>(Error.Validation("ProjectKey.Format", "Project key must be uppercase letters/numbers, starting with letter"));

        return Result.Success(new ProjectKey(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(ProjectKey key) => key.Value;
}
