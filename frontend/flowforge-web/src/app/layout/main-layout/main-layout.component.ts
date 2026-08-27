import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';
import { NotificationsRealtimeService } from '../../core/services/notifications-realtime.service';
import { NotificationsService } from '../../shared/services/notifications.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    MatIconModule, MatMenuModule, MatButtonModule
  ],
  templateUrl: './main-layout.component.html'
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  auth = inject(AuthService);
  private notificationsRealtime = inject(NotificationsRealtimeService);
  notifications = inject(NotificationsService);

  primaryNav = [
    { path: '/inbox',     label: 'Inbox',     icon: 'inbox' },
    { path: '/my-work',   label: 'My Work',   icon: 'check_circle' },
    { path: '/dashboard', label: 'Dashboard', icon: 'space_dashboard' }
  ];

  workspaceNav = [
    { path: '/projects', label: 'Projects', icon: 'folder' },
    { path: '/team',     label: 'Team',     icon: 'group' },
    { path: '/settings', label: 'Settings', icon: 'settings' }
  ];

  get initials(): string {
    const u = this.auth.user();
    return u ? `${u.firstName[0]}${u.lastName[0]}`.toUpperCase() : '';
  }

  get workspaceInitial(): string {
    return this.auth.tenant()?.name?.[0]?.toUpperCase() ?? 'W';
  }

  async ngOnInit() {
    this.notifications.refreshUnreadCount();

    await this.notificationsRealtime.startConnection();
    this.notificationsRealtime.notificationReceived$.subscribe(() => {
      this.notifications.unreadCount.update(count => count + 1);
    });
  }

  async ngOnDestroy() {
    await this.notificationsRealtime.stop();
  }

  logout() { this.auth.logout(); }
}
