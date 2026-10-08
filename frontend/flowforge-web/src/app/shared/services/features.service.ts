import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export const FEATURE_AI = 'ai-assistant';
export const FEATURE_ATTACHMENTS = 'attachments';
export const FEATURE_SPRINTS = 'sprints';

/**
 * Which features the signed-in user's workspace can use (set by system admins). Everything is
 * treated as on until the answer arrives, and if the request fails, so a hiccup here never hides
 * working features. The server enforces the real rule; this only keeps the UI honest.
 */
@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private http = inject(HttpClient);
  private flags = signal<Record<string, boolean>>({});

  load() {
    this.http.get<Record<string, boolean>>(`${environment.apiUrl}/features`).subscribe({
      next: flags => this.flags.set(flags),
      error: () => this.flags.set({})
    });
  }

  isEnabled(key: string): boolean {
    return this.flags()[key] ?? true;
  }
}
