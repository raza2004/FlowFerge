namespace FlowForge.Application.AI.DTOs;

public record TaskBreakdownSuggestionDto(List<string> Subtasks);

public record AssigneeSuggestionDto(Guid UserId, string FullName, string Reasoning);

public record ProjectAiSummaryDto(string Summary, DateTime GeneratedAt);

public record ApplyTaskBreakdownRequest(List<string> SubtaskTitles);

public record BlockerSignalDto(
    Guid? TaskId,
    string? TaskNumber,
    string Title,
    string Kind,
    string Severity,
    string Detail
);

/// <param name="Signals">Rule-based findings; always present, even when the AI is unavailable.</param>
/// <param name="AiSummary">The AI's prioritised read of the signals, or null if it couldn't run.</param>
public record ProjectBlockersDto(
    List<BlockerSignalDto> Signals,
    int OpenTasks,
    string? AiSummary,
    List<string> Recommendations,
    bool AiAvailable,
    string? AiMessage,
    DateTime GeneratedAt
);
