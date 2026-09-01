import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest, UserInfo, TenantInfo } from '../../shared/models/auth.models';

const ACCESS_TOKEN_KEY  = 'ff_access_token';
const REFRESH_TOKEN_KEY = 'ff_refresh_token';
const USER_KEY          = 'ff_user';
const TENANT_KEY        = 'ff_tenant';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http   = inject(HttpClient);
  private router = inject(Router);

  user   = signal<UserInfo | null>(this.load<UserInfo>(USER_KEY));
  tenant = signal<TenantInfo | null>(this.load<TenantInfo>(TENANT_KEY));

  /** @deprecated use user() */
  get currentUser() { return this.user; }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, req).pipe(
      tap(res => this.storeSession(res))
    );
  }

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, req).pipe(
      tap(res => this.storeSession(res))
    );
  }

  logout(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(TENANT_KEY);
    this.user.set(null);
    this.tenant.set(null);
    this.router.navigate(['/auth/login']);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  /** Applies a partial update (e.g. a saved preference) to the stored user, keeping the signal and localStorage in sync. */
  patchUser(partial: Partial<UserInfo>): void {
    const updated = { ...this.user(), ...partial } as UserInfo;
    this.user.set(updated);
    localStorage.setItem(USER_KEY, JSON.stringify(updated));
  }

  /** Applies a partial update (e.g. a saved Slack webhook) to the stored tenant, keeping the signal and localStorage in sync. */
  patchTenant(partial: Partial<TenantInfo>): void {
    const updated = { ...this.tenant(), ...partial } as TenantInfo;
    this.tenant.set(updated);
    localStorage.setItem(TENANT_KEY, JSON.stringify(updated));
  }

  isAuthenticated(): boolean {
    return !!this.getAccessToken();
  }

  private storeSession(res: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY,  res.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY,   JSON.stringify(res.user));
    localStorage.setItem(TENANT_KEY, JSON.stringify(res.tenant));
    this.user.set(res.user);
    this.tenant.set(res.tenant);
  }

  private load<T>(key: string): T | null {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : null;
  }
}
