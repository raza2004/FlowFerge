using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress, string? UserAgent) : IRequest<Result<AuthResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IJwtTokenGenerator jwtGenerator)
    {
        _uow = uow;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var token = await _uow.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (token == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.InvalidToken", "Refresh token not found"));

        if (!token.IsActive)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.InvalidToken", "Refresh token expired or revoked"));

        var user = await _uow.Users.GetByIdAsync(token.UserId, ct);
        if (user == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.InvalidToken", "User not found"));

        var newRefreshToken = RefreshToken.Create(user.Id, 7, request.IpAddress, request.UserAgent);
        await _uow.RefreshTokens.AddAsync(newRefreshToken, ct);

        token.Revoke("Replaced by new token", newRefreshToken.Id);
        _uow.RefreshTokens.Update(token);

        await _uow.SaveChangesAsync(ct);

        var memberships = await _uow.Memberships.GetByUserAsync(user.Id, ct);
        var primaryMembership = memberships.FirstOrDefault(m => m.IsActive);

        var (accessToken, expiresAt) = _jwtGenerator.GenerateAccessTokenWithExpiry(user, primaryMembership);

        TenantDto? tenantDto = null;
        if (primaryMembership != null)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(primaryMembership.TenantId, ct);
            if (tenant != null)
                tenantDto = new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl, tenant.PlanTier, tenant.IsActive);
        }

        return Result.Success(new AuthResponse(
            accessToken,
            newRefreshToken.Token,
            expiresAt,
            new UserDto(user.Id, user.Email.Value, user.FirstName, user.LastName, user.FullName, user.AvatarUrl, user.IsSystemAdmin, user.IsEmailVerified),
            tenantDto
        ));
    }
}
