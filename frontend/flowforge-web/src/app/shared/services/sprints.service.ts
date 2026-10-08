import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SprintDetailDto, SprintDto, SprintForm, SprintTaskDto } from '../models/project.models';

/** Sprints run on whole UTC days; the date inputs give yyyy-MM-dd, so send them as midnight UTC. */
const asUtc = (date: string) => `${date}T00:00:00Z`;

@Injectable({ providedIn: 'root' })
export class SprintsService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  getSprints(projectId: string): Observable<SprintDto[]> {
    return this.http.get<SprintDto[]>(`${this.api}/projects/${projectId}/sprints`);
  }

  getBacklog(projectId: string): Observable<SprintTaskDto[]> {
    return this.http.get<SprintTaskDto[]>(`${this.api}/projects/${projectId}/backlog`);
  }

  getDetail(sprintId: string): Observable<SprintDetailDto> {
    return this.http.get<SprintDetailDto>(`${this.api}/sprints/${sprintId}`);
  }

  create(projectId: string, form: SprintForm): Observable<SprintDto> {
    return this.http.post<SprintDto>(`${this.api}/projects/${projectId}/sprints`, this.body(form));
  }

  update(sprintId: string, form: SprintForm): Observable<void> {
    return this.http.put<void>(`${this.api}/sprints/${sprintId}`, this.body(form));
  }

  start(sprintId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/sprints/${sprintId}/start`, null);
  }

  complete(sprintId: string, retrospectiveNotes: string | null, moveIncompleteToSprintId: string | null): Observable<void> {
    return this.http.post<void>(`${this.api}/sprints/${sprintId}/complete`, { retrospectiveNotes, moveIncompleteToSprintId });
  }

  cancel(sprintId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/sprints/${sprintId}/cancel`, null);
  }

  saveRetrospective(sprintId: string, notes: string | null): Observable<void> {
    return this.http.put<void>(`${this.api}/sprints/${sprintId}/retrospective`, { notes });
  }

  assignTask(taskId: string, sprintId: string | null): Observable<void> {
    return this.http.put<void>(`${this.api}/tasks/${taskId}/sprint`, { sprintId });
  }

  private body(form: SprintForm) {
    return { name: form.name.trim(), goal: form.goal?.trim() || null, startDate: asUtc(form.startDate), endDate: asUtc(form.endDate) };
  }
}
