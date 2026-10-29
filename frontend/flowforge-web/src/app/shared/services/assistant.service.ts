import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpDownloadProgressEvent, HttpErrorResponse, HttpEventType } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AssistantDocument, AssistantError, AssistantStreamEvent, AssistantUsage, Citation } from '../models/assistant.models';

/**
 * Talks to the .NET API only. The browser never calls the AI service directly: the API checks the
 * signed in person, their workspace and their role before anything reaches it.
 */
@Injectable({ providedIn: 'root' })
export class AssistantService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/ai`;

  /**
   * Streams an answer. The server sends server-sent events: sources first, then words as they are
   * written, then a final event with usage. A POST cannot use EventSource, so the raw text is
   * read as it downloads and split into events here.
   */
  stream(question: string): Observable<AssistantStreamEvent> {
    return new Observable<AssistantStreamEvent>(subscriber => {
      let consumed = 0;

      const drain = (text: string) => {
        while (true) {
          const end = text.indexOf('\n\n', consumed);
          if (end < 0) break;
          const block = text.slice(consumed, end);
          consumed = end + 2;
          const line = block.split('\n').find(l => l.startsWith('data: '));
          if (!line) continue;
          try {
            const event = this.toEvent(JSON.parse(line.slice(6)));
            if (event) subscriber.next(event);
          } catch {
            // A half written event is picked up again on the next chunk.
          }
        }
      };

      const sub = this.http.post(`${this.base}/ask/stream`, { question }, {
        observe: 'events', reportProgress: true, responseType: 'text'
      }).subscribe({
        next: event => {
          if (event.type === HttpEventType.DownloadProgress) {
            drain((event as HttpDownloadProgressEvent).partialText ?? '');
          } else if (event.type === HttpEventType.Response) {
            drain(event.body ?? '');
            subscriber.complete();
          }
        },
        error: err => subscriber.error(this.friendly(err))
      });

      return () => sub.unsubscribe();
    });
  }

  listDocuments(): Observable<AssistantDocument[]> {
    return this.http.get<AssistantDocument[]>(`${this.base}/documents`);
  }

  uploadDocument(file: File): Observable<AssistantDocument> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<AssistantDocument>(`${this.base}/documents`, form);
  }

  deleteDocument(documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/documents/${encodeURIComponent(documentId)}`);
  }

  /** Turns an HTTP failure into a sentence a person can act on. */
  friendly(err: unknown): AssistantError {
    if (!(err instanceof HttpErrorResponse)) return new AssistantError('Something went wrong. Please try again.', 0);

    let detail = '';
    try {
      const body = typeof err.error === 'string' ? JSON.parse(err.error) : err.error;
      detail = body?.detail ?? '';
    } catch {
      detail = '';
    }

    switch (err.status) {
      case 0: return new AssistantError('Cannot reach the server. Check your connection and try again.', 0);
      case 400: return new AssistantError(detail || 'That request could not be processed.', 400);
      case 403: return new AssistantError(detail || 'Only workspace owners and admins can do that.', 403);
      case 413: return new AssistantError('That file is too large. The limit is 25 MB.', 413);
      case 429: return new AssistantError(detail || 'The assistant is busy. Wait a moment and try again.', 429);
      case 503: return new AssistantError(detail || 'The assistant is temporarily unavailable. Please try again in a moment.', 503);
      default: return new AssistantError(detail || 'Something went wrong. Please try again.', err.status);
    }
  }

  // ---- the Python service speaks snake_case, the rest of the API camelCase ----

  private toEvent(raw: any): AssistantStreamEvent | null {
    switch (raw?.type) {
      case 'sources':
        return { type: 'sources', sources: (raw.sources ?? []).map((s: any) => this.toCitation(s)) };
      case 'token':
        return { type: 'token', text: String(raw.text ?? '') };
      case 'done':
        return { type: 'done', usage: raw.usage ? this.toUsage(raw.usage) : null };
      default:
        return null;
    }
  }

  private toCitation(s: any): Citation {
    return {
      documentId: s.document_id ?? s.documentId ?? '',
      documentName: s.document_name ?? s.documentName ?? 'Document',
      page: s.page ?? 1,
      content: s.content ?? '',
      score: s.score ?? 0
    };
  }

  private toUsage(u: any): AssistantUsage {
    return {
      model: u.model ?? '',
      promptTokens: u.prompt_tokens ?? u.promptTokens ?? 0,
      completionTokens: u.completion_tokens ?? u.completionTokens ?? 0,
      costUsd: u.cost_usd ?? u.costUsd ?? 0,
      latencyMs: u.latency_ms ?? u.latencyMs ?? 0
    };
  }
}
