using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;

namespace FlowForge.Application.Identity.Services;

/// <summary>
/// One place that decides which workspace a session is scoped to and issues the token pair
/// for it. The chosen workspace is stored on the refresh token, so refreshing keeps you in
/// the workspace you switched to instead of silently jumping back to your first one.
/// </summary>
public interface IAuthSessionFactory
{
    /// <summary>
    /// Returns the user's active membership in <paramref name="preferredTenantId"/> if it's still
    /// valid, otherwise their first membership in a non-suspended workspace, otherwise null.
    /// </summary>
    Task<Membership?> ResolveMembershipAsync(Guid userId, Guid? preferredTenantId, CancellationToken ct);

    /// <summary>Creates and saves a refresh token for the membership's workspace and returns the full auth response.</summary>
    Task<AuthResponse> CreateSessionAsync(User user, Membership? membership, string? ipAddress, string? userAgent, CancellationToken ct);
}

public class AuthSessionFactory : IAuthSessionFactory
{
    private const int RefreshTokenDays = 7;

    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenGenerator _jwt;

    public AuthSessionFactory(IUnitOfWork uow, IJwtTokenGenerator jwt)
    {
        _uow = uow;
        _jwt = jwt;
    }

    public async Task<Membership?> ResolveMembershipAsync(Guid userId, Guid? preferredTenantId, CancellationToken ct)
    {
        var memberships = (await _uow.Memberships.GetByUserAsync(userId, ct))
            .Where(m => m.IsActive && m.Tenant.IsActive)
            .OrderBy(m => m.JoinedAt)
            .ToList();

        return memberships.FirstOrDefault(m => m.TenantId == preferredTenantId) ?? memberships.FirstOrDefault();
    }

    public async Task<AuthResponse> CreateSessionAsync(User user, Membership? membership, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var refreshToken = RefreshToken.Create(user.Id, RefreshTokenDays, ipAddress, userAgent, membership?.TenantId);
        await _uow.RefreshTokens.AddAsync(refreshToken, ct);
        await _uow.SaveChangesAsync(ct);

        var (accessToken, expiresAt) = _jwt.GenerateAccessTokenWithExpiry(user, membership);

        TenantDto? tenantDto = null;
        if (membership != null)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(membership.TenantId, ct);
            if (tenant != null)
                tenantDto = new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl, tenant.PlanTier, tenant.IsActive, tenant.SlackWebhookUrl);
        }

        return new AuthResponse(
            accessToken,
            refreshToken.Token,
            expiresAt,
            new UserDto(user.Id, user.Email.Value, user.FirstName, user.LastName, user.FullName, user.AvatarUrl,
                user.IsSystemAdmin, user.IsEmailVerified, user.EmailNotificationsEnabled),
            tenantDto);
    }
}
