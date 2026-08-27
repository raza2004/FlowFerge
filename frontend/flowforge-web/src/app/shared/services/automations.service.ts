import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AutomationRuleDto, CreateAutomationRuleRequest } from '../models/automation.models';

@Injectable({ providedIn: 'root' })
export class AutomationsService {
  private http = inject(HttpClient);

  getForProject(projectId: string): Observable<AutomationRuleDto[]> {
    return this.http.get<AutomationRuleDto[]>(`${environment.apiUrl}/projects/${projectId}/automations`);
  }

  create(projectId: string, req: CreateAutomationRuleRequest): Observable<AutomationRuleDto> {
    return this.http.post<AutomationRuleDto>(`${environment.apiUrl}/projects/${projectId}/automations`, req);
  }

  toggle(ruleId: string, enabled: boolean): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/automations/${ruleId}/toggle`, { enabled });
  }

  delete(ruleId: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/automations/${ruleId}`);
  }
}
