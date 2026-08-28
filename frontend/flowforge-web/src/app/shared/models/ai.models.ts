export interface TaskBreakdownSuggestionDto {
  subtasks: string[];
}

export interface AssigneeSuggestionDto {
  userId: string;
  fullName: string;
  reasoning: string;
}

export interface ProjectAiSummaryDto {
  summary: string;
  generatedAt: string;
}

export interface ApplyTaskBreakdownRequest {
  subtaskTitles: string[];
}
