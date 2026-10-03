import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BoardDto, BoardListDto, ListSettings } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class BoardsService {
  private http = inject(HttpClient);

  getBoard(boardId: string): Observable<BoardDto> {
    return this.http.get<BoardDto>(`${environment.apiUrl}/boards/${boardId}`);
  }

  getProjectBoards(projectId: string): Observable<BoardDto[]> {
    return this.http.get<BoardDto[]>(`${environment.apiUrl}/projects/${projectId}/boards`);
  }

  createList(boardId: string, settings: Omit<ListSettings, 'isDoneColumn'>): Observable<BoardListDto> {
    return this.http.post<BoardListDto>(`${environment.apiUrl}/boards/${boardId}/lists`, settings);
  }

  updateList(boardId: string, listId: string, settings: ListSettings): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/boards/${boardId}/lists/${listId}`, settings);
  }

  deleteList(boardId: string, listId: string, moveTasksTo: string | null): Observable<void> {
    const params: Record<string, string> = moveTasksTo ? { moveTasksTo } : {};
    return this.http.delete<void>(`${environment.apiUrl}/boards/${boardId}/lists/${listId}`, { params });
  }

  reorderLists(boardId: string, listIds: string[]): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/boards/${boardId}/lists/order`, { listIds });
  }
}
