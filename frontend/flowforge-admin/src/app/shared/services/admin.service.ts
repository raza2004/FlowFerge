import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminAuditLogDto, AdminStatsDto, AdminTenantDto, AdminUserDto, FeatureFlagDto } from '../models/admin.models';

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

  getFeatureFlags(): Observable<FeatureFlagDto[]> {
    return this.http.get<FeatureFlagDto[]>(`${environment.apiUrl}/admin/features`);
  }

  setFeatureEnabled(key: string, enabled: boolean): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/admin/features/${key}`, { enabled });
  }

  /** enabled = null removes the workspace's override so the global setting applies again. */
  setFeatureOverride(key: string, tenantId: string, enabled: boolean | null): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/admin/features/${key}/tenants/${tenantId}`, { enabled });
  }
}
