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

export interface BlockerSignalDto {
  taskId: string | null;
  taskNumber: string | null;
  title: string;
  kind: 'Overdue' | 'DueSoonUnassigned' | 'Stale' | 'Overloaded';
  severity: 'High' | 'Medium' | 'Low';
  detail: string;
}

export interface ProjectBlockersDto {
  signals: BlockerSignalDto[];
  openTasks: number;
  aiSummary: string | null;
  recommendations: string[];
  aiAvailable: boolean;
  aiMessage: string | null;
  generatedAt: string;
}
