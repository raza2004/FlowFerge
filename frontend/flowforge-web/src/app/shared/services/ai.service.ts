import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TaskBreakdownSuggestionDto, AssigneeSuggestionDto, ProjectAiSummaryDto, ApplyTaskBreakdownRequest
} from '../models/ai.models';

@Injectable({ providedIn: 'root' })
export class AiService {
  private http = inject(HttpClient);

  suggestBreakdown(taskId: string): Observable<TaskBreakdownSuggestionDto> {
    return this.http.get<TaskBreakdownSuggestionDto>(`${environment.apiUrl}/tasks/${taskId}/ai/breakdown`);
  }

  applyBreakdown(taskId: string, req: ApplyTaskBreakdownRequest): Observable<number> {
    return this.http.post<number>(`${environment.apiUrl}/tasks/${taskId}/ai/breakdown/apply`, req);
  }

  suggestAssignee(taskId: string): Observable<AssigneeSuggestionDto> {
    return this.http.get<AssigneeSuggestionDto>(`${environment.apiUrl}/tasks/${taskId}/ai/suggest-assignee`);
  }

  getProjectSummary(projectId: string): Observable<ProjectAiSummaryDto> {
    return this.http.get<ProjectAiSummaryDto>(`${environment.apiUrl}/projects/${projectId}/ai/summary`);
  }
}
