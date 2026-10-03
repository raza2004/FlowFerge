using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Application.Identity.Services;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

/// <summary>
/// Creates an account for an invited email address and joins the workspace in one step. Unlike
/// normal registration it doesn't create a new workspace - the invitation already says which one.
/// </summary>
public record RegisterWithInvitationCommand(
    string Token,
    string FirstName,
    string LastName,
    string Password,
    string? IpAddress,
    string? UserAgent
) : IRequest<Result<AuthResponse>>;

public class RegisterWithInvitationCommandValidator : AbstractValidator<RegisterWithInvitationCommand>
{
    public RegisterWithInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Password must contain uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a number");
    }
}

public class RegisterWithInvitationCommandHandler : IRequestHandler<RegisterWithInvitationCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthSessionFactory _sessions;

    public RegisterWithInvitationCommandHandler(IUnitOfWork uow, IPasswordHasher passwordHasher, IAuthSessionFactory sessions)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterWithInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _uow.Invitations.GetByTokenAsync(request.Token, ct);
        if (invitation == null)
            return Result.Failure<AuthResponse>(Error.NotFound("Invitation.NotFound", "This invitation link isn't valid"));

        if (await _uow.Users.EmailExistsAsync(invitation.Email, ct))
            return Result.Failure<AuthResponse>(Error.Conflict("User.EmailExists",
                "An account with this email already exists. Sign in to accept the invitation."));

        var userResult = User.Create(invitation.Email, request.FirstName, request.LastName, _passwordHasher.HashPassword(request.Password));
        if (userResult.IsFailure) return Result.Failure<AuthResponse>(userResult.Error);

        var user = userResult.Value;
        user.MarkEmailVerifiedByInvitation();
        await _uow.Users.AddAsync(user, ct);

        var joinResult = await InvitationAcceptance.JoinAsync(_uow, invitation, user, ct);
        if (joinResult.IsFailure) return Result.Failure<AuthResponse>(joinResult.Error);

        // CreateSessionAsync saves everything above (user, membership, invitation) together with the refresh token.
        return Result.Success(await _sessions.CreateSessionAsync(user, joinResult.Value, request.IpAddress, request.UserAgent, ct));
    }
}
