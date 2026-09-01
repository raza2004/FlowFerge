import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private http = inject(HttpClient);

  updateSlackWebhook(webhookUrl: string | null): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/settings/slack-webhook`, { webhookUrl });
  }

  updateNotificationPreferences(emailNotificationsEnabled: boolean): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/settings/notification-preferences`, { emailNotificationsEnabled });
  }
}
