using FlowForge.Domain.Identity.Enums;
using FlowForge.Domain.Identity.Events;
using FlowForge.Domain.Identity.ValueObjects;
using FlowForge.Shared.Primitives;
using FlowForge.Shared.Results;

namespace FlowForge.Domain.Identity;

public sealed class User : AggregateRoot
{
    public Email Email { get; private set; } = null!;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public string? PhoneNumber { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public string? EmailVerificationToken { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public bool IsSystemAdmin { get; private set; }

    private readonly List<Membership> _memberships = new();
    public IReadOnlyCollection<Membership> Memberships => _memberships.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsLocked => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
    public bool IsEmailVerified => EmailVerifiedAt.HasValue;

    private User() { }

    public static Result<User> Create(Email email, string firstName, string lastName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100)
            return Result.Failure<User>(Error.Validation("User.InvalidFirstName", "First name must be 1-100 chars"));

        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100)
            return Result.Failure<User>(Error.Validation("User.InvalidLastName", "Last name must be 1-100 chars"));

        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>(Error.Validation("User.InvalidPassword", "Password hash required"));

        return Result.Success(new User
        {
            Email = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            Status = UserStatus.Pending,
            EmailVerificationToken = Guid.NewGuid().ToString("N")
        });
    }

    public void RecordSuccessfulLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
        Touch();
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
            LockedUntil = DateTime.UtcNow.AddMinutes(15);
        Touch();
    }

    public Result VerifyEmail(string token)
    {
        if (EmailVerificationToken != token)
            return Result.Failure(Error.Validation("User.InvalidToken", "Verification token is invalid"));

        EmailVerifiedAt = DateTime.UtcNow;
        EmailVerificationToken = null;
        Status = UserStatus.Active;
        Touch();
        return Result.Success();
    }

    public void UpdateProfile(string firstName, string lastName, string? bio, string? phoneNumber, string? avatarUrl)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Bio = bio;
        PhoneNumber = phoneNumber;
        AvatarUrl = avatarUrl;
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        Touch();
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
        Touch();
    }

    public void Reactivate()
    {
        Status = UserStatus.Active;
        Touch();
    }

    public void PromoteToSystemAdmin()
    {
        IsSystemAdmin = true;
        Touch();
    }
}
