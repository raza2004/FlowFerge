import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ProjectDto, ProjectSummaryDto, CreateProjectRequest } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private http = inject(HttpClient);

  getAll(includeArchived = false): Observable<ProjectSummaryDto[]> {
    return this.http.get<ProjectSummaryDto[]>(`${environment.apiUrl}/projects`, {
      params: { includeArchived: String(includeArchived) }
    });
  }

  getById(id: string): Observable<ProjectDto> {
    return this.http.get<ProjectDto>(`${environment.apiUrl}/projects/${id}`);
  }

  create(req: CreateProjectRequest): Observable<ProjectDto> {
    return this.http.post<ProjectDto>(`${environment.apiUrl}/projects`, req);
  }
}
