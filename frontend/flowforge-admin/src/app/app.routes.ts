import { Routes } from '@angular/router';
import { adminAuthGuard } from './core/guards/admin-auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },

  {
    path: '',
    canActivate: [adminAuthGuard],
    loadComponent: () => import('./layout/admin-layout/admin-layout.component').then(m => m.AdminLayoutComponent),
    children: [
      { path: 'dashboard',   loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'tenants',     loadComponent: () => import('./features/tenants/tenants.component').then(m => m.TenantsComponent) },
      { path: 'users',       loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent) },
      { path: 'audit-logs',  loadComponent: () => import('./features/audit-logs/audit-logs.component').then(m => m.AuditLogsComponent) }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
