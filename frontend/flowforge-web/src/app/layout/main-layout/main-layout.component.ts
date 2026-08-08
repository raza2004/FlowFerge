import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule, RouterOutlet, RouterLink, RouterLinkActive,
    MatIconModule, MatMenuModule, MatButtonModule
  ],
  templateUrl: './main-layout.component.html'
})
export class MainLayoutComponent {
  auth = inject(AuthService);

  primaryNav = [
    { path: '/inbox',     label: 'Inbox',     icon: 'inbox',          badge: 0 },
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

  logout() { this.auth.logout(); }
}
