using FlowForge.Shared.Results;

namespace FlowForge.Application.Common.Abstractions;

public record SubtaskSuggestion(List<string> Titles);

public record AssigneeCandidate(Guid UserId, string FullName, int OpenTaskCount);
public record AssigneeSuggestion(Guid SuggestedUserId, string Reasoning);

public record ProjectSummaryInput(
    string ProjectName,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks,
    List<string> InProgressTaskTitles,
    List<string> OverdueTaskTitles
);

/// <summary>
/// Abstraction over the AI provider (implemented with the OpenAI SDK in Infrastructure).
/// Application code never touches OpenAI types directly, and every method returns a
/// Result so a missing/invalid API key is a normal, handled failure - not an exception
/// that takes down a request.
/// </summary>
public interface IAiService
{
    Task<Result<SubtaskSuggestion>> SuggestSubtasksAsync(string taskTitle, string? taskDescription, CancellationToken ct = default);

    Task<Result<AssigneeSuggestion>> SuggestAssigneeAsync(string taskTitle, string? taskDescription, List<AssigneeCandidate> candidates, CancellationToken ct = default);

    Task<Result<string>> SummarizeProjectAsync(ProjectSummaryInput input, CancellationToken ct = default);
}
