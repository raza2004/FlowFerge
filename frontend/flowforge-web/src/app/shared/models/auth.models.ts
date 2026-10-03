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
  emailNotificationsEnabled: boolean;
}

export interface TenantInfo {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  planTier: string;
  isActive: boolean;
  slackWebhookUrl: string | null;
}

export interface InvitationDto {
  id: string;
  email: string;
  role: string;
  invitedByName: string;
  createdAt: string;
  expiresAt: string;
  inviteUrl: string;
}

export interface InvitationPreviewDto {
  tenantName: string;
  email: string;
  role: string;
  invitedByName: string;
  status: 'pending' | 'accepted' | 'revoked' | 'expired';
  accountExists: boolean;
}

export interface WorkspaceDto {
  tenantId: string;
  name: string;
  slug: string;
  role: string;
  isCurrent: boolean;
}

/** Highest privilege first; mirrors FlowForge.Domain.Identity.Enums.MembershipRole. */
export const MEMBERSHIP_ROLES = ['Owner', 'Admin', 'Manager', 'Member', 'Guest'] as const;

export interface TenantMemberDto {
  userId: string;
  fullName: string;
  email: string;
  avatarUrl: string | null;
  role: string;
  joinedAt: string;
}
