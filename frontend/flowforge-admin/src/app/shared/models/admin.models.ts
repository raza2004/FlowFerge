export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserInfo;
  tenant: unknown;
}

export interface UserInfo {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  isSystemAdmin: boolean;
}

export interface AdminStatsDto {
  totalTenants: number;
  activeTenants: number;
  totalUsers: number;
  totalProjects: number;
}

export interface AdminTenantDto {
  id: string;
  name: string;
  slug: string;
  planTier: string;
  isActive: boolean;
  suspensionReason: string | null;
  memberCount: number;
  projectCount: number;
  createdAt: string;
}

export interface AdminUserDto {
  id: string;
  email: string;
  fullName: string;
  isSystemAdmin: boolean;
  isEmailVerified: boolean;
  status: string;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AdminAuditLogDto {
  id: string;
  tenantId: string;
  tenantName: string | null;
  userId: string | null;
  userName: string | null;
  action: string;
  entityType: string;
  severity: string;
  createdAt: string;
}
