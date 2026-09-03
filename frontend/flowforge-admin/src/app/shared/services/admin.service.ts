import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminAuditLogDto, AdminStatsDto, AdminTenantDto, AdminUserDto } from '../models/admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);

  getStats(): Observable<AdminStatsDto> {
    return this.http.get<AdminStatsDto>(`${environment.apiUrl}/admin/stats`);
  }

  getTenants(): Observable<AdminTenantDto[]> {
    return this.http.get<AdminTenantDto[]>(`${environment.apiUrl}/admin/tenants`);
  }

  suspendTenant(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/admin/tenants/${id}/suspend`, { reason });
  }

  reactivateTenant(id: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/admin/tenants/${id}/reactivate`, {});
  }

  getUsers(): Observable<AdminUserDto[]> {
    return this.http.get<AdminUserDto[]>(`${environment.apiUrl}/admin/users`);
  }

  suspendUser(id: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/admin/users/${id}/suspend`, {});
  }

  reactivateUser(id: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/admin/users/${id}/reactivate`, {});
  }

  getAuditLogs(): Observable<AdminAuditLogDto[]> {
    return this.http.get<AdminAuditLogDto[]>(`${environment.apiUrl}/admin/audit-logs`);
  }
}
