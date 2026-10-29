namespace FlowForge.Application.AI.Assistant;

/// <summary>A passage of an uploaded document that supports an answer, so a user can verify it.</summary>
public record CitationDto(string DocumentId, string DocumentName, int Page, string Content, double Score);

public record AiUsageDto(string Model, int PromptTokens, int CompletionTokens, double CostUsd, int LatencyMs);

public record AssistantAnswerDto(string Answer, IReadOnlyList<CitationDto> Sources, AiUsageDto Usage, string RetrievalMode);

public record AssistantDocumentDto(string DocumentId, string FileName, int Pages, int Chunks);
