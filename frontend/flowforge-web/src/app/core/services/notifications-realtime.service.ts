import { Injectable, inject, signal } from '@angular/core';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { NotificationDto } from '../../shared/models/notification.models';

/**
 * Connects once per authenticated session (started from MainLayoutComponent, which wraps
 * every route behind the auth guard) so the unread badge and Inbox update live from
 * anywhere in the app, not just while the Inbox page happens to be open.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsRealtimeService {
  private auth = inject(AuthService);
  private connection: HubConnection | null = null;

  notificationReceived$ = new Subject<NotificationDto>();

  /** Live connection state, shown in the status bar so "is realtime working?" is never a guess. */
  connected = signal(false);

  async startConnection(): Promise<void> {
    if (this.connection?.state === 'Connected') return;

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/notifications`, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('NotificationReceived', (dto: NotificationDto) => this.notificationReceived$.next(dto));
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onclose(() => this.connected.set(false));

    await this.connection.start();
    this.connected.set(true);
  }

  async stop(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
    this.connected.set(false);
  }
}
