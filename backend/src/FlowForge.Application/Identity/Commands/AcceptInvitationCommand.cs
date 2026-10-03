using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

/// <summary>
/// Accepts an invitation as the signed-in user and returns a session scoped to the new workspace.
/// The signed-in account's email must match the invited one, so a forwarded link can't be used
/// by someone else.
/// </summary>
public record AcceptInvitationCommand(string Token) : IRequest<Result<AuthResponse>>;

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthSessionFactory _sessions;

    public AcceptInvitationCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IAuthSessionFactory sessions)
    {
        _uow = uow;
        _currentUser = currentUser;
        _sessions = sessions;
    }

    public async Task<Result<AuthResponse>> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.Required", "Sign in to accept this invitation"));

        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId.Value, ct);
        if (user == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.Required", "Sign in to accept this invitation"));

        var invitation = await _uow.Invitations.GetByTokenAsync(request.Token, ct);
        if (invitation == null)
            return Result.Failure<AuthResponse>(Error.NotFound("Invitation.NotFound", "This invitation link isn't valid"));

        // Email is a value object without ==/!= overloads, so compare with Equals (by value).
        if (!invitation.Email.Equals(user.Email))
            return Result.Failure<AuthResponse>(Error.Forbidden("Invitation.WrongAccount",
                $"This invitation was sent to {invitation.Email.Value}. Sign in with that account to accept it."));

        var joinResult = await InvitationAcceptance.JoinAsync(_uow, invitation, user, ct);
        if (joinResult.IsFailure) return Result.Failure<AuthResponse>(joinResult.Error);

        user.MarkEmailVerifiedByInvitation();
        return Result.Success(await _sessions.CreateSessionAsync(user, joinResult.Value, _currentUser.IpAddress, _currentUser.UserAgent, ct));
    }
}
