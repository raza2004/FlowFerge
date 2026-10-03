using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress, string? UserAgent) : IRequest<Result<AuthResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IAuthSessionFactory _sessions;

    public RefreshTokenCommandHandler(IUnitOfWork uow, IAuthSessionFactory sessions)
    {
        _uow = uow;
        _sessions = sessions;
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

        // Suspension takes effect at the next refresh at the latest, not just at the next login.
        if (user.Status == UserStatus.Suspended)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.AccountSuspended", "This account has been suspended"));

        token.Revoke("Rotated");

        // Stay in the workspace this session was using, unless access to it was lost meanwhile.
        var membership = await _sessions.ResolveMembershipAsync(user.Id, token.TenantId, ct);
        return Result.Success(await _sessions.CreateSessionAsync(user, membership, request.IpAddress, request.UserAgent, ct));
    }
}
