namespace FlowForge.Application.AI.DTOs;

public record TaskBreakdownSuggestionDto(List<string> Subtasks);

public record AssigneeSuggestionDto(Guid UserId, string FullName, string Reasoning);

public record ProjectAiSummaryDto(string Summary, DateTime GeneratedAt);

public record ApplyTaskBreakdownRequest(List<string> SubtaskTitles);
