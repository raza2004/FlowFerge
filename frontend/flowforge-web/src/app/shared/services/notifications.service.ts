import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NotificationDto } from '../models/notification.models';

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private http = inject(HttpClient);

  /** Shared across the app (sidebar badge + Inbox page) - same pattern as AuthService.user/tenant. */
  unreadCount = signal(0);

  getMine(): Observable<NotificationDto[]> {
    return this.http.get<NotificationDto[]>(`${environment.apiUrl}/notifications`);
  }

  refreshUnreadCount(): void {
    this.http.get<number>(`${environment.apiUrl}/notifications/unread-count`)
      .subscribe(count => this.unreadCount.set(count));
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/notifications/${id}/read`, {}).pipe(
      tap(() => this.unreadCount.update(c => Math.max(0, c - 1)))
    );
  }
}
