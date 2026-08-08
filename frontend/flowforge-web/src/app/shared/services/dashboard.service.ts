import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardStatsDto, MyTaskDto } from '../models/project.models';
import { TenantMemberDto } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private http = inject(HttpClient);

  getStats(): Observable<DashboardStatsDto> {
    return this.http.get<DashboardStatsDto>(`${environment.apiUrl}/dashboard/stats`);
  }

  getMyTasks(): Observable<MyTaskDto[]> {
    return this.http.get<MyTaskDto[]>(`${environment.apiUrl}/dashboard/my-tasks`);
  }

  getTeamMembers(): Observable<TenantMemberDto[]> {
    return this.http.get<TenantMemberDto[]>(`${environment.apiUrl}/users/members`);
  }
}
