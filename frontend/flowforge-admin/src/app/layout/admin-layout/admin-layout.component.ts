import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminAuthService } from '../../core/services/admin-auth.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, MatIconModule, MatButtonModule, MatTooltipModule],
  templateUrl: './admin-layout.component.html'
})
export class AdminLayoutComponent {
  auth = inject(AdminAuthService);

  nav = [
    { path: '/dashboard',   label: 'Dashboard',   icon: 'space_dashboard' },
    { path: '/tenants',     label: 'Tenants',     icon: 'apartment' },
    { path: '/users',       label: 'Users',       icon: 'group' },
    { path: '/audit-logs',  label: 'Audit logs',  icon: 'history' }
  ];

  logout() { this.auth.logout(); }
}
