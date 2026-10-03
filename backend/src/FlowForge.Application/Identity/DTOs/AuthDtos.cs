namespace FlowForge.Application.Identity.DTOs;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string TenantName,
    string TenantSlug
);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserDto User,
    TenantDto? Tenant
);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? AvatarUrl,
    bool IsSystemAdmin,
    bool IsEmailVerified,
    bool EmailNotificationsEnabled
);

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string PlanTier,
    bool IsActive,
    string? SlackWebhookUrl
);

public record MembershipDto(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string Role,
    DateTime JoinedAt
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record CreateInvitationRequest(string Email, string Role);

public record RegisterWithInvitationRequest(string FirstName, string LastName, string Password);

public record SwitchWorkspaceRequest(Guid TenantId, string? RefreshToken);

public record ChangeMemberRoleRequest(string Role);

public record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    string InvitedByName,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string InviteUrl
);

/// <summary>What someone opening an invite link sees, before they're logged in.</summary>
public record InvitationPreviewDto(
    string TenantName,
    string Email,
    string Role,
    string InvitedByName,
    string Status,
    bool AccountExists
);

public record WorkspaceDto(
    Guid TenantId,
    string Name,
    string Slug,
    string Role,
    bool IsCurrent
);

public record TenantMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string? AvatarUrl,
    string Role,
    DateTime JoinedAt
);
