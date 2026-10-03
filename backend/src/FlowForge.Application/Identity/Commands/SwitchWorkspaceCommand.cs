using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

/// <summary>
/// Issues a new session scoped to another workspace the user belongs to. The current refresh
/// token, if provided, is revoked so the old session doesn't linger alongside the new one.
/// </summary>
public record SwitchWorkspaceCommand(Guid TenantId, string? RefreshToken) : IRequest<Result<AuthResponse>>;

public class SwitchWorkspaceCommandHandler : IRequestHandler<SwitchWorkspaceCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthSessionFactory _sessions;

    public SwitchWorkspaceCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IAuthSessionFactory sessions)
    {
        _uow = uow;
        _currentUser = currentUser;
        _sessions = sessions;
    }

    public async Task<Result<AuthResponse>> Handle(SwitchWorkspaceCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.Required", "Not signed in"));

        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
        if (user == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.Required", "Not signed in"));

        var membership = await _sessions.ResolveMembershipAsync(user.Id, request.TenantId, ct);
        if (membership == null || membership.TenantId != request.TenantId)
            return Result.Failure<AuthResponse>(Error.Forbidden("Workspace.NotMember", "You don't have access to that workspace"));

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var current = await _uow.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
            if (current != null && current.UserId == user.Id && current.IsActive)
                current.Revoke("Switched workspace");
        }

        return Result.Success(await _sessions.CreateSessionAsync(user, membership, _currentUser.IpAddress, _currentUser.UserAgent, ct));
    }
}
