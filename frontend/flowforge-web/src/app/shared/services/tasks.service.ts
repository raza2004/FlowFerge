import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TaskCardDto, CreateTaskRequest, TaskDetailDto, UpdateTaskRequest,
  TaskCommentDto, SubtaskDto, LabelDto, TimeEntryDto
} from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class TasksService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/tasks`;

  createTask(req: CreateTaskRequest): Observable<TaskCardDto> {
    return this.http.post<TaskCardDto>(this.base, req);
  }

  moveTask(taskId: string, newListId: string, newPosition: number, boardId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${taskId}/move`, { newListId, newPosition, boardId });
  }

  assignTask(taskId: string, assigneeId: string, boardId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${taskId}/assign`, { assigneeId, boardId });
  }

  getDetail(taskId: string): Observable<TaskDetailDto> {
    return this.http.get<TaskDetailDto>(`${this.base}/${taskId}`);
  }

  update(taskId: string, req: UpdateTaskRequest): Observable<void> {
    return this.http.put<void>(`${this.base}/${taskId}`, req);
  }

  delete(taskId: string, boardId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${taskId}`, { params: { boardId } });
  }

  addComment(taskId: string, content: string, mentionedUserIds: string[], boardId: string): Observable<TaskCommentDto> {
    return this.http.post<TaskCommentDto>(`${this.base}/${taskId}/comments`, { content, mentionedUserIds }, { params: { boardId } });
  }

  editComment(commentId: string, content: string): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/comments/${commentId}`, { content });
  }

  deleteComment(commentId: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/comments/${commentId}`);
  }

  createSubtask(parentTaskId: string, title: string, boardId: string): Observable<SubtaskDto> {
    return this.http.post<SubtaskDto>(`${this.base}/${parentTaskId}/subtasks`, { title }, { params: { boardId } });
  }

  setLabel(taskId: string, labelId: string, attach: boolean, boardId: string): Observable<void> {
    const url = `${this.base}/${taskId}/labels/${labelId}`;
    return attach
      ? this.http.put<void>(url, null, { params: { boardId } })
      : this.http.delete<void>(url, { params: { boardId } });
  }

  setWatching(taskId: string, watch: boolean): Observable<void> {
    const url = `${this.base}/${taskId}/watch`;
    return watch ? this.http.put<void>(url, null) : this.http.delete<void>(url);
  }

  logTime(taskId: string, hours: number, workDate: string | null, note: string | null): Observable<TimeEntryDto> {
    return this.http.post<TimeEntryDto>(`${this.base}/${taskId}/time`, { hours, workDate, note });
  }

  deleteTimeEntry(entryId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/time/${entryId}`);
  }

  getProjectLabels(projectId: string): Observable<LabelDto[]> {
    return this.http.get<LabelDto[]>(`${environment.apiUrl}/projects/${projectId}/labels`);
  }

  createLabel(projectId: string, name: string, color: string): Observable<LabelDto> {
    return this.http.post<LabelDto>(`${environment.apiUrl}/projects/${projectId}/labels`, { name, color });
  }
}
