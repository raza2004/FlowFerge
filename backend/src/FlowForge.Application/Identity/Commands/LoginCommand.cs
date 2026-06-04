using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.ValueObjects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent
) : IRequest<Result<AuthResponse>>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public LoginCommandHandler(IUnitOfWork uow, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtGenerator)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));

        var user = await _uow.Users.GetByEmailAsync(emailResult.Value, ct);
        if (user == null)
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));

        if (user.IsLocked)
            return Result.Failure<AuthResponse>(Error.Forbidden("Auth.AccountLocked", $"Account locked until {user.LockedUntil:u}"));

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync(ct);
            return Result.Failure<AuthResponse>(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));
        }

        user.RecordSuccessfulLogin();

        var memberships = await _uow.Memberships.GetByUserAsync(user.Id, ct);
        var primaryMembership = memberships.FirstOrDefault(m => m.IsActive);

        var refreshToken = RefreshToken.Create(user.Id, 7, request.IpAddress, request.UserAgent);
        await _uow.RefreshTokens.AddAsync(refreshToken, ct);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

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
            refreshToken.Token,
            expiresAt,
            new UserDto(user.Id, user.Email.Value, user.FirstName, user.LastName, user.FullName, user.AvatarUrl, user.IsSystemAdmin, user.IsEmailVerified),
            tenantDto
        ));
    }
}
