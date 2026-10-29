export interface Citation {
  documentId: string;
  documentName: string;
  page: number;
  content: string;
  score: number;
}

export interface AssistantUsage {
  model: string;
  promptTokens: number;
  completionTokens: number;
  costUsd: number;
  latencyMs: number;
}

export interface AssistantDocument {
  documentId: string;
  fileName: string;
  pages: number;
  chunks: number;
}

/** What the streaming endpoint emits, already converted to camelCase. */
export type AssistantStreamEvent =
  | { type: 'sources'; sources: Citation[] }
  | { type: 'token'; text: string }
  | { type: 'done'; usage: AssistantUsage | null };

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  sources: Citation[];
  status: 'streaming' | 'done' | 'error';
  error?: string;
  usage?: AssistantUsage | null;
}

/** Error whose message is safe to show to a person as is. */
export class AssistantError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
  }
}
