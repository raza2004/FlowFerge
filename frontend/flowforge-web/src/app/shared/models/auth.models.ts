export interface LoginRequest {
  email: string;
  password: string;
  ipAddress?: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  tenantName: string;
  tenantSlug: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: UserInfo;
  tenant: TenantInfo | null;
}

export interface UserInfo {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  avatarUrl: string | null;
  isSystemAdmin: boolean;
  isEmailVerified: boolean;
}

export interface TenantInfo {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  planTier: string;
  isActive: boolean;
}

export interface TenantMemberDto {
  userId: string;
  fullName: string;
  email: string;
  avatarUrl: string | null;
  role: string;
  joinedAt: string;
}
