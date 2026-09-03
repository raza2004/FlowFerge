namespace FlowForge.Application.Admin.DTOs;

public record AdminTenantDto(
    Guid Id,
    string Name,
    string Slug,
    string PlanTier,
    bool IsActive,
    string? SuspensionReason,
    int MemberCount,
    int ProjectCount,
    DateTime CreatedAt
);

public record AdminUserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsSystemAdmin,
    bool IsEmailVerified,
    string Status,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record AdminStatsDto(
    int TotalTenants,
    int ActiveTenants,
    int TotalUsers,
    int TotalProjects
);

public record AdminAuditLogDto(
    Guid Id,
    Guid TenantId,
    string? TenantName,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string Severity,
    DateTime CreatedAt
);
