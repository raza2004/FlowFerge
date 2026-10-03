import { Injectable, inject } from '@angular/core';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface TaskMovedEvent {
  taskId: string;
  newListId: string;
  newPosition: number;
}

@Injectable({ providedIn: 'root' })
export class SignalrService {
  private auth = inject(AuthService);
  private connection: HubConnection | null = null;

  taskMoved$ = new Subject<TaskMovedEvent>();
  taskCreated$ = new Subject<any>();
  taskUpdated$ = new Subject<{ taskId: string }>();
  taskDeleted$ = new Subject<{ taskId: string }>();
  boardChanged$ = new Subject<{ boardId: string }>();

  async startConnection(): Promise<void> {
    if (this.connection?.state === 'Connected') return;

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/board`, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('TaskMoved', (event: TaskMovedEvent) => this.taskMoved$.next(event));
    this.connection.on('TaskCreated', (event: any) => this.taskCreated$.next(event));
    this.connection.on('TaskUpdated', (event: { taskId: string }) => this.taskUpdated$.next(event));
    this.connection.on('TaskDeleted', (event: { taskId: string }) => this.taskDeleted$.next(event));
    this.connection.on('BoardChanged', (event: { boardId: string }) => this.boardChanged$.next(event));

    await this.connection.start();
  }

  async joinBoard(boardId: string): Promise<void> {
    if (!this.connection) await this.startConnection();
    await this.connection?.invoke('JoinBoard', boardId);
  }

  async leaveBoard(boardId: string): Promise<void> {
    if (!this.connection) return;
    await this.connection.invoke('LeaveBoard', boardId);
  }

  async stop(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
  }
}
