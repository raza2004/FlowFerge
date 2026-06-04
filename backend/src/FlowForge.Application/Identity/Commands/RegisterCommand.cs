using AutoMapper;
using FluentValidation;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Identity.DTOs;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity;
using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.ValueObjects;
using FlowForge.Shared.Results;
using MediatR;

namespace FlowForge.Application.Identity.Commands;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string TenantName,
    string TenantSlug,
    string? IpAddress,
    string? UserAgent
) : IRequest<Result<AuthResponse>>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Password must contain uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a number");
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(50)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must be lowercase letters, numbers, hyphens");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly IMapper _mapper;

    public RegisterCommandHandler(IUnitOfWork uow, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtGenerator, IMapper mapper)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtGenerator = jwtGenerator;
        _mapper = mapper;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure) return Result.Failure<AuthResponse>(emailResult.Error);

        if (await _uow.Users.EmailExistsAsync(emailResult.Value, ct))
            return Result.Failure<AuthResponse>(Error.Conflict("User.EmailExists", "Email already registered"));

        if (await _uow.Tenants.SlugExistsAsync(request.TenantSlug, ct))
            return Result.Failure<AuthResponse>(Error.Conflict("Tenant.SlugExists", "Workspace URL already taken"));

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var passwordHash = _passwordHasher.HashPassword(request.Password);
            var userResult = User.Create(emailResult.Value, request.FirstName, request.LastName, passwordHash);
            if (userResult.IsFailure)
            {
                await _uow.RollbackTransactionAsync(ct);
                return Result.Failure<AuthResponse>(userResult.Error);
            }

            var user = userResult.Value;
            await _uow.Users.AddAsync(user, ct);

            var tenantResult = Tenant.Create(request.TenantName, request.TenantSlug, user.Id);
            if (tenantResult.IsFailure)
            {
                await _uow.RollbackTransactionAsync(ct);
                return Result.Failure<AuthResponse>(tenantResult.Error);
            }

            var tenant = tenantResult.Value;
            await _uow.Tenants.AddAsync(tenant, ct);

            var membershipResult = Membership.Create(user.Id, tenant.Id, MembershipRole.Owner);
            if (membershipResult.IsFailure)
            {
                await _uow.RollbackTransactionAsync(ct);
                return Result.Failure<AuthResponse>(membershipResult.Error);
            }

            await _uow.Memberships.AddAsync(membershipResult.Value, ct);

            var refreshToken = RefreshToken.Create(user.Id, 7, request.IpAddress, request.UserAgent);
            await _uow.RefreshTokens.AddAsync(refreshToken, ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);

            var (accessToken, expiresAt) = _jwtGenerator.GenerateAccessTokenWithExpiry(user, membershipResult.Value);

            return Result.Success(new AuthResponse(
                accessToken,
                refreshToken.Token,
                expiresAt,
                new UserDto(user.Id, user.Email.Value, user.FirstName, user.LastName, user.FullName, user.AvatarUrl, user.IsSystemAdmin, user.IsEmailVerified),
                new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl, tenant.PlanTier, tenant.IsActive)
            ));
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
