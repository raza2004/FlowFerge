import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TaskCardDto, CreateTaskRequest } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class TasksService {
  private http = inject(HttpClient);

  createTask(req: CreateTaskRequest): Observable<TaskCardDto> {
    return this.http.post<TaskCardDto>(`${environment.apiUrl}/tasks`, req);
  }

  moveTask(taskId: string, newListId: string, newPosition: number, boardId: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/tasks/${taskId}/move`, {
      newListId, newPosition, boardId
    });
  }
}
