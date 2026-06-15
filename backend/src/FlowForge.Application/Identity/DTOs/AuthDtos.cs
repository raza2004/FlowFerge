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
    bool IsEmailVerified
);

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string PlanTier,
    bool IsActive
);

public record MembershipDto(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string Role,
    DateTime JoinedAt
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record InviteUserRequest(string Email, string FirstName, string LastName, string Role);

public record AcceptInvitationRequest(string Token, string Password);

public record TenantMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string? AvatarUrl,
    string Role,
    DateTime JoinedAt
);
