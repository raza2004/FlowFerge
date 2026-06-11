import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BoardDto } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class BoardsService {
  private http = inject(HttpClient);

  getBoard(boardId: string): Observable<BoardDto> {
    return this.http.get<BoardDto>(`${environment.apiUrl}/boards/${boardId}`);
  }

  getProjectBoards(projectId: string): Observable<BoardDto[]> {
    return this.http.get<BoardDto[]>(`${environment.apiUrl}/projects/${projectId}/boards`);
  }
}
