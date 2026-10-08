namespace FlowForge.Application.Features;

public record FeatureDefinition(string Key, string Name, string Description);

/// <summary>
/// Every feature that can be switched from the admin panel. Adding one here is all it takes to
/// make it appear there (the flag row is created on startup, defaulting to on).
/// </summary>
public static class FeatureKeys
{
    public const string AiAssistant = "ai-assistant";
    public const string Attachments = "attachments";
    public const string Sprints = "sprints";

    public static readonly IReadOnlyList<FeatureDefinition> All = new[]
    {
        new FeatureDefinition(AiAssistant, "AI assistant", "Task breakdown, assignee suggestions, project summaries and risk analysis."),
        new FeatureDefinition(Attachments, "File attachments", "Uploading files to tasks. Files already uploaded stay downloadable when this is off."),
        new FeatureDefinition(Sprints, "Sprints", "Creating and starting sprints. Existing sprints stay readable when this is off.")
    };
}
