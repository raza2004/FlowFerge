import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { InvitationDto, TenantMemberDto } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class TeamService {
  private http = inject(HttpClient);

  getMembers(): Observable<TenantMemberDto[]> {
    return this.http.get<TenantMemberDto[]>(`${environment.apiUrl}/users/members`);
  }

  changeRole(userId: string, role: string): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/users/members/${userId}/role`, { role });
  }

  removeMember(userId: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/users/members/${userId}`);
  }

  getInvitations(): Observable<InvitationDto[]> {
    return this.http.get<InvitationDto[]>(`${environment.apiUrl}/invitations`);
  }

  invite(email: string, role: string): Observable<InvitationDto> {
    return this.http.post<InvitationDto>(`${environment.apiUrl}/invitations`, { email, role });
  }

  revokeInvitation(id: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/invitations/${id}`);
  }
}
